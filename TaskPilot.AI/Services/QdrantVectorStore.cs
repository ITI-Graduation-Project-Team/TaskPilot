using System;
using TaskPilot.Models.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Options;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services
{
    public class QdrantVectorStore : IVectorStore
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly QdrantClient _client;
        private readonly QdrantOptions _options;
        private readonly ILogger<QdrantVectorStore> _logger;

        public QdrantVectorStore(
            IEmbeddingService embeddingService, 
            IOptions<QdrantOptions> options,
            ILogger<QdrantVectorStore> logger)
        {
            _embeddingService = embeddingService;
            _logger = logger;
            
            _options = options.Value;

            var url = _options.Url;
            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            if (!string.IsNullOrEmpty(url))
            {
                var uri = new Uri(url);
                var port = uri.IsDefaultPort ? 6334 : uri.Port;
                var https = uri.Scheme == "https";

                _logger.LogInformation("Initializing Qdrant client for URL: {Url}, Host: {Host}, Port: {Port}, Mode: gRPC", url, uri.Host, port);
                _client = new QdrantClient(host: uri.Host, port: port, https: https, apiKey: _options.ApiKey);
            }
            else
            {
                _logger.LogWarning("Qdrant URL is empty. Initializing with localhost.");
                _client = new QdrantClient("localhost", 6334);
            }
        }

        public async Task EnsureCollectionsAsync(CancellationToken cancellationToken = default)
        {
            var collectionTypes = Enum.GetValues<KnowledgeCollectionType>();
            
            foreach (var type in collectionTypes)
            {
                var collectionName = GetCollectionName(type);
                
                var collectionExists = await _client.CollectionExistsAsync(collectionName, cancellationToken);

                if (!collectionExists)
                {
                    await _client.CreateCollectionAsync(
                        collectionName,
                        vectorsConfig: new VectorParams
                        {
                            Size = 1536, // match the embedding model's output size
                            Distance = Distance.Cosine
                        },
                        cancellationToken: cancellationToken);

                    _logger.LogInformation("Collection created: {CollectionName}", collectionName);
                }
                else
                {
                    _logger.LogInformation("Collection exists: {CollectionName}", collectionName);
                }

                await EnsurePayloadIndexAsync(collectionName, "RequirementSessionId", PayloadSchemaType.Uuid, cancellationToken);
                await EnsurePayloadIndexAsync(collectionName, "ProjectId", PayloadSchemaType.Uuid, cancellationToken);
                await EnsurePayloadIndexAsync(collectionName, "CompanyId", PayloadSchemaType.Uuid, cancellationToken);
                await EnsurePayloadIndexAsync(collectionName, "Category", PayloadSchemaType.Keyword, cancellationToken);
                await EnsurePayloadIndexAsync(collectionName, "DocumentId", PayloadSchemaType.Uuid, cancellationToken);
            }
        }

        private string GetCollectionName(KnowledgeCollectionType type)
        {
            return type switch
            {
                KnowledgeCollectionType.ProjectPolicies => string.IsNullOrWhiteSpace(_options.Collections.ProjectPolicies) ? "taskpilot_project_policies" : _options.Collections.ProjectPolicies,
                KnowledgeCollectionType.CompanyPolicies => string.IsNullOrWhiteSpace(_options.Collections.CompanyPolicies) ? "taskpilot_company_policies" : _options.Collections.CompanyPolicies,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        private async Task EnsurePayloadIndexAsync(
            string collectionName,
            string fieldName,
            PayloadSchemaType schemaType,
            CancellationToken cancellationToken)
        {
            try
            {
                var collectionInfo = await _client.GetCollectionInfoAsync(collectionName, cancellationToken);
                if (collectionInfo.PayloadSchema.ContainsKey(fieldName))
                {
                    _logger.LogInformation("{FieldName} index exists in {CollectionName}.", fieldName, collectionName);
                    return;
                }

                await _client.CreatePayloadIndexAsync(
                    collectionName,
                    fieldName,
                    schemaType,
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Creating {FieldName} payload index...", fieldName);
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists || (ex.Status.Detail != null && ex.Status.Detail.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
            {
                // Index already exists — this is expected on subsequent startups, not an error
                _logger.LogInformation("{FieldName} index exists.", fieldName);
            }
            catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("{FieldName} index exists.", fieldName);
            }
        }

        public async Task UpsertAsync(
            KnowledgeCollectionType collectionType,
            List<KnowledgeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            if (chunks == null || chunks.Count == 0) return;

            var collectionName = GetCollectionName(collectionType);
            var pointIds = chunks.Select(c => (PointId)c.Id).ToList();

            IReadOnlyList<RetrievedPoint> existingPoints = new List<RetrievedPoint>();
            try
            {
                existingPoints = await _client.RetrieveAsync(
                    collectionName,
                    pointIds,
                    withVectors: true,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve existing points. Proceeding to embed all. Collection: {CollectionName}", collectionName);
            }

            var existingPointsMap = existingPoints.ToDictionary(p => p.Id.Uuid, p => p);

            var textsToEmbed = new List<string>();
            foreach (var chunk in chunks)
            {
                if (!existingPointsMap.ContainsKey(chunk.Id.ToString()))
                {
                    textsToEmbed.Add(chunk.Content);
                }
            }

            List<float[]> generatedEmbeddings = new List<float[]>();
            if (textsToEmbed.Any())
            {
                _logger.LogInformation("Generating {Count} new embeddings.", textsToEmbed.Count);
                generatedEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToEmbed, cancellationToken);
            }
            else
            {
                _logger.LogInformation("All {Count} embeddings already exist. Bypassing embedding generation.", chunks.Count);
            }

            var newPoints = new List<PointStruct>();
            var existingPointIds = new List<Guid>();
            int newEmbeddingIndex = 0;

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var payload = new Dictionary<string, Value>
                {
                    { "DocumentId", chunk.DocumentId.ToString() },
                    { "Category", ((int)chunk.Category).ToString() },
                    { "SourceFile", chunk.SourceFile },
                    { "DocumentType", chunk.DocumentType },
                    { "Content", chunk.Content },
                    { "ChunkIndex", chunk.ChunkIndex },
                    { "CreatedAt", chunk.CreatedAt.ToString("o") }
                };

                if (chunk.RequirementSessionId.HasValue)
                {
                    payload["RequirementSessionId"] = chunk.RequirementSessionId.Value.ToString();
                }

                if (chunk.ProjectId.HasValue)
                {
                    payload["ProjectId"] = chunk.ProjectId.Value.ToString();
                }

                if (chunk.CompanyId.HasValue)
                {
                    payload["CompanyId"] = chunk.CompanyId.Value.ToString();
                }

                if (existingPointsMap.ContainsKey(chunk.Id.ToString()))
                {
                    existingPointIds.Add(chunk.Id);
                    // Update payload for existing points
                    await _client.SetPayloadAsync(
                        collectionName,
                        payload,
                        ids: new[] { chunk.Id },
                        wait: true,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    var embedding = generatedEmbeddings[newEmbeddingIndex++];
                    var point = new PointStruct
                    {
                        Id = chunk.Id,
                        Vectors = embedding,
                        Payload = { payload }
                    };
                    newPoints.Add(point);
                }
            }

            if (newPoints.Any())
            {
                await _client.UpsertAsync(collectionName, newPoints, cancellationToken: cancellationToken);
            }
        }

        public async Task<List<KnowledgeChunk>> SearchAsync(
            KnowledgeCollectionType collectionType,
            Guid? requirementSessionId,
            Guid? projectId,
            Guid? companyId,
            string queryText,
            int topK = 5,
            float scoreThreshold = 0.75f,
            DocumentCategory? categoryFilter = null,
            CancellationToken cancellationToken = default)
        {
            if (requirementSessionId == null && projectId == null && companyId == null)
            {
                throw new ArgumentException("Either RequirementSessionId, ProjectId, or CompanyId must be provided to ensure multi-tenant isolation.");
            }

            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(queryText, cancellationToken);

            var conditions = new List<Condition>();

            if (requirementSessionId.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "RequirementSessionId",
                        Match = new Match { Keyword = requirementSessionId.Value.ToString() }
                    }
                });
            }

            if (projectId.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "ProjectId",
                        Match = new Match { Keyword = projectId.Value.ToString() }
                    }
                });
            }

            if (companyId.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "CompanyId",
                        Match = new Match { Keyword = companyId.Value.ToString() }
                    }
                });
            }

            if (categoryFilter.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "Category",
                        Match = new Match { Keyword = ((int)categoryFilter.Value).ToString() }
                    }
                });
            }

            var filter = new Filter
            {
                Must = { conditions }
            };

            var collectionName = GetCollectionName(collectionType);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var searchResult = await _client.SearchAsync(
                collectionName,
                queryEmbedding,
                filter: filter,
                limit: (ulong)topK,
                scoreThreshold: scoreThreshold,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation("Qdrant Search in {CollectionName} | Query: '{QueryText}' | RequirementSessionId: {RequirementSessionId} | ProjectId: {ProjectId} | CompanyId: {CompanyId} | TopK: {TopK} | Threshold: {ScoreThreshold} | Returned {Count} results | SearchDuration: {SearchDuration}ms", 
                collectionName, queryText, requirementSessionId, projectId, companyId, topK, scoreThreshold, searchResult.Count, stopwatch.ElapsedMilliseconds);

            var resultChunks = new List<KnowledgeChunk>();
            foreach (var item in searchResult)
            {
                var payload = item.Payload;
                var chunkIndex = (int)payload["ChunkIndex"].IntegerValue;
                
                _logger.LogInformation("Returned: Chunk {Index} - Score {Score}", chunkIndex, item.Score);

                var chunk = new KnowledgeChunk
                {
                    Id = new Guid(item.Id.Uuid),
                    DocumentId = Guid.Parse(payload["DocumentId"].StringValue),
                    Category = (DocumentCategory)int.Parse(payload["Category"].StringValue),
                    SourceFile = payload.TryGetValue("SourceFile", out var sf) ? sf.StringValue : string.Empty,
                    DocumentType = payload.TryGetValue("DocumentType", out var dt) ? dt.StringValue : string.Empty,
                    Content = payload["Content"].StringValue,
                    ChunkIndex = chunkIndex,
                    CreatedAt = DateTime.Parse(payload["CreatedAt"].StringValue)
                };

                if (payload.TryGetValue("RequirementSessionId", out var reqVal) && Guid.TryParse(reqVal.StringValue, out var reqId))
                {
                    chunk.RequirementSessionId = reqId;
                }

                if (payload.TryGetValue("ProjectId", out var projectVal) && Guid.TryParse(projectVal.StringValue, out var pId))
                {
                    chunk.ProjectId = pId;
                }

                if (payload.TryGetValue("CompanyId", out var companyVal) && Guid.TryParse(companyVal.StringValue, out var cId))
                {
                    chunk.CompanyId = cId;
                }

                resultChunks.Add(chunk);
            }

            return resultChunks;
        }

        public async Task DeleteAsync(
            KnowledgeCollectionType collectionType, 
            Guid documentId, 
            Guid? requirementSessionId,
            Guid? projectId,
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(collectionType);

            var conditions = new List<Condition>
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "DocumentId",
                        Match = new Match { Keyword = documentId.ToString() }
                    }
                }
            };

            if (requirementSessionId.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "RequirementSessionId",
                        Match = new Match { Keyword = requirementSessionId.Value.ToString() }
                    }
                });
            }

            if (projectId.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "ProjectId",
                        Match = new Match { Keyword = projectId.Value.ToString() }
                    }
                });
            }

            if (companyId.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "CompanyId",
                        Match = new Match { Keyword = companyId.Value.ToString() }
                    }
                });
            }

            var filter = new Filter { Must = { conditions } };

            await _client.DeleteAsync(
                collectionName,
                filter,
                wait: true,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Deleted document {DocumentId} chunks from {CollectionName}", documentId, collectionName);
        }

        public async Task PromoteKnowledgeAsync(
            KnowledgeCollectionType collectionType,
            Guid projectId,
            IEnumerable<Guid> chunkIds,
            CancellationToken cancellationToken = default)
        {
            var chunkIdList = chunkIds.ToList();
            if (!chunkIdList.Any()) return;

            var collectionName = GetCollectionName(collectionType);
            var payload = new Dictionary<string, Value>
            {
                { "ProjectId", projectId.ToString() }
            };

            await _client.SetPayloadAsync(
                collectionName,
                payload,
                ids: chunkIdList,
                wait: true,
                cancellationToken: cancellationToken);

            await _client.DeletePayloadAsync(
                collectionName,
                keys: new[] { "RequirementSessionId" },
                ids: chunkIdList,
                wait: true,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Promoted {Count} chunks to ProjectId {ProjectId} in {CollectionName}", chunkIdList.Count, projectId, collectionName);
        }
    }
}
