using System.Collections.Generic;
using System.Linq;

namespace TaskPilot.AI.Models.Requirements
{
    public class ExtractedRequirements
    {
        public List<string>
            BusinessRequirements
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            TechnicalRequirements
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            Constraints
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            Integrations
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            ScaleRequirements
        {
            get;
            set;
        }
        =
            new();
        public IEnumerable<string>
            AsEnumerable()
        {
            return BusinessRequirements

                .Concat(
                    TechnicalRequirements)

                .Concat(
                    Constraints)

                .Concat(
                    Integrations)

                .Concat(
                    ScaleRequirements);
        }

        public string
            ToPromptText()
        {
            return string.Join(
                "\n",
                AsEnumerable());
        }

        public void
            MergeFrom(
                RequirementExtractionResult
                    extracted)
        {
            MergeList(
                BusinessRequirements,
                extracted
                    .BusinessRequirements);

            MergeList(
                TechnicalRequirements,
                extracted
                    .TechnicalRequirements);

            MergeList(
                Constraints,
                extracted
                    .Constraints);

            MergeList(
                Integrations,
                extracted
                    .Integrations);

            MergeList(
                ScaleRequirements,
                extracted
                    .ScaleRequirements);
        }

        public void
            MergeFrom(
                ExtractedRequirements
                    extracted)
        {
            MergeList(
                BusinessRequirements,
                extracted
                    .BusinessRequirements);

            MergeList(
                TechnicalRequirements,
                extracted
                    .TechnicalRequirements);

            MergeList(
                Constraints,
                extracted
                    .Constraints);

            MergeList(
                Integrations,
                extracted
                    .Integrations);

            MergeList(
                ScaleRequirements,
                extracted
                    .ScaleRequirements);
        }

        private static void
            MergeList(
                List<string> target,
                List<string> source)
        {
            foreach (var item
                     in source)
            {
                if (!target.Contains(
                     item,
                     StringComparer
                         .OrdinalIgnoreCase))
                {
                    target.Add(item);
                }
            }
        }
    }
}

