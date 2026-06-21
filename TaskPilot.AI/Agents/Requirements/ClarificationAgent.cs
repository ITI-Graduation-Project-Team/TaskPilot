using Microsoft.SemanticKernel;
using System.Text.Json;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Questions;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Models.Workflow;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class ClarificationQuestionResult
    {
        public string Question { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    public class ClarificationAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;

        public ClarificationAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
        }

        public async Task<List<ClarificationQuestion>>
            GenerateAsync(
                RequirementSession session)
        {
            var kernel =
                _kernelService
                    .CreateKernel(
                        ModelConstants
                            .CheapModel);

            // Load YAML prompt
            var prompt =
                await _promptLoader
                    .LoadAsync(
                        "Requirements/Clarification.yaml");

            // Create YAML function
            var function =
                KernelFunctionYaml
                    .FromPromptYaml(
                        prompt);

            // Create balanced arguments
            var arguments =
                KernelArgumentsFactory
                    .CreateBalancedArguments();

            arguments["ambiguities"] =
                string.Join(
                    "\n",
                    session
                        .DetectedAmbiguities);

            arguments["criticalMissingAreas"] =
                string.Join(
                    "\n",
                    session
                        .CompletenessReport?
                        .CriticalMissingAreas
                        ?? []);

            arguments["conversationHistory"] =
                string.Join(
                    "\n",
                    session
                        .ConversationHistory
                        .Select(x =>
                            $"{x.Role}: {x.Message}"));

            // Invoke
            var result =
                await kernel.InvokeAsync(
                    function,
                    arguments);

            var json =
                result.ToString()
                      .Trim();

            var questionResults =
                JsonSerializer.Deserialize
                    <List<ClarificationQuestionResult>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive =
                                true
                        });

            var questions =
                questionResults?

                    .Select(q =>
                    {
                        var category = QuestionCategory.General;
                        if (Enum.TryParse<QuestionCategory>(q.Category.Trim(), true, out var cat))
                        {
                            category = cat;
                        }

                        return new ClarificationQuestion
                        {
                            Id =
                                Guid.NewGuid(),

                            Question =
                                q.Question
                                    .Trim(),

                            Category =
                                category,

                            IsAnswered =
                                false
                        };
                    })

                    .Where(q =>
                        !string.IsNullOrWhiteSpace(
                            q.Question))

                    .DistinctBy(q =>
                        q.Question
                            .ToLower())

                    .ToList()

                ??

                new List<ClarificationQuestion>();

            // Split compound questions
            questions =
       SplitCompoundQuestions(
           questions);

            // Remove already existing questions in QuestionPool
            questions =
                questions

                    .Where(q =>
                        !session
                            .QuestionPool
                            .Any(existing =>

                                existing.Question.Equals(
                                    q.Question,
                                    StringComparison
                                        .OrdinalIgnoreCase)))

                    .ToList();

            // Remove semantic duplicates
            questions =
                questions

                    .Where(q =>

                        !IsSemanticallyDuplicate(
                            q.Question,
                            session
                                .QuestionPool))

                    .ToList();

            // Prevent excessive refinement loops
            var saturatedCategories =
                session
                    .QuestionPool
                    .Where(q =>
                        q.IsAnswered)

                    .Select(q =>
                        q.Category.ToString())

                    .Distinct(
                        StringComparer
                            .OrdinalIgnoreCase)

                    .ToHashSet(
                        StringComparer
                            .OrdinalIgnoreCase);

            // Remove already-satisfied categories
            questions =
                questions

                    .Where(q =>

                        !saturatedCategories
                            .Contains(
                                q.Category.ToString()))

                    .ToList();

            // Save decision using extension method
            session.AddDecision(
                nameof(ClarificationAgent),
                $"Generated {questions.Count} clarification questions");

            return questions;
        }

        private static List<ClarificationQuestion>
    SplitCompoundQuestions(
        List<ClarificationQuestion>
            questions)
        {
            var result =
                new List<ClarificationQuestion>();

            foreach (var question
                     in questions)
            {
                var text =
                    question.Question;

                var normalized =
                    text.ToLower();

                // users + transactions
                if (normalized.Contains("users")
                    &&
                    normalized.Contains("transactions"))
                {
                    result.Add(
                        new ClarificationQuestion
                        {
                            Id =
                                Guid.NewGuid(),

                            Question =
                                "What is the expected number of concurrent users?",

                            Category =
                                question.Category
                        });

                    result.Add(
                        new ClarificationQuestion
                        {
                            Id =
                                Guid.NewGuid(),

                            Question =
                                "What is the expected transaction volume?",

                            Category =
                                question.Category
                        });

                    continue;
                }

                // users + data volume
                if (normalized.Contains("users")
                    &&
                    normalized.Contains("data volume"))
                {
                    result.Add(
                        new ClarificationQuestion
                        {
                            Id =
                                Guid.NewGuid(),

                            Question =
                                "What is the expected number of concurrent users?",

                            Category =
                                question.Category
                        });

                    result.Add(
                        new ClarificationQuestion
                        {
                            Id =
                                Guid.NewGuid(),

                            Question =
                                "What is the expected data volume?",

                            Category =
                                question.Category
                        });

                    continue;
                }

                result.Add(question);
            }

            return result;
        }
        private static bool
    IsSemanticallyDuplicate(
        string question,
        List<ClarificationQuestion>
            existingQuestions)
        {
            var normalized =
                question
                    .ToLower();

            foreach (var existing
                     in existingQuestions)
            {
                var existingNormalized =
                    existing.Question
                        .ToLower();

                // timeline duplicates
                if (normalized.Contains("timeline")
                    &&
                    existingNormalized.Contains("timeline"))
                {
                    return true;
                }

                // compliance duplicates
                if ((normalized.Contains("compliance")
                     ||
                     normalized.Contains("privacy"))

                    &&

                    (existingNormalized.Contains("compliance")
                     ||
                     existingNormalized.Contains("privacy")))
                {
                    return true;
                }

                // authentication duplicates
                if ((normalized.Contains("authentication")
                     ||
                     normalized.Contains("access"))

                    &&

                    (existingNormalized.Contains("authentication")
                     ||
                     existingNormalized.Contains("access")))
                {
                    return true;
                }

                // realtime duplicates
                if ((normalized.Contains("real-time")
                     ||
                     normalized.Contains("alerts"))

                    &&

                    (existingNormalized.Contains("real-time")
                     ||
                     existingNormalized.Contains("alerts")))
                {
                    return true;
                }

                // integration duplicates
                if (normalized.Contains("integration")
                    &&
                    existingNormalized.Contains("integration"))
                {
                    return true;
                }

                // scale duplicates
                if ((normalized.Contains("users")
                     ||
                     normalized.Contains("scale"))

                    &&

                    (existingNormalized.Contains("users")
                     ||
                     existingNormalized.Contains("scale")))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
