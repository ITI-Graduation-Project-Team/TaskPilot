using System;
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
        private readonly string _collectionName;
        private readonly ILogger<QdrantVectorStore> _logger;

        public QdrantVectorStore(
            IEmbeddingService embeddingService, 
            IOptions<QdrantOptions> options,
            ILogger<QdrantVectorStore> logger)
        {
            _embeddingService = embeddingService;
            _logger = logger;
            
            var qdrantOptions = options.Value;
            _collectionName = string.IsNullOrWhiteSpace(qdrantOptions.CollectionName) ? "taskpilot_knowledge" : qdrantOptions.CollectionName;

            var url = qdrantOptions.Url;
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
                _client = new QdrantClient(host: uri.Host, port: port, https: https, apiKey: qdrantOptions.ApiKey);
            }
            else
            {
                _logger.LogWarning("Qdrant URL is empty. Initializing with localhost.");
                _client = new QdrantClient("localhost", 6334);
            }
        }

        public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
        {
            // Step 1: Ensure the collection itself exists
            var collectionExists = await _client.CollectionExistsAsync(
                _collectionName, cancellationToken);

            if (!collectionExists)
            {
                await _client.CreateCollectionAsync(
                    _collectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size = 1536, // match the embedding model's output size
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Collection created: {CollectionName}", _collectionName);
            }
            else
            {
                _logger.LogInformation("Collection exists: {CollectionName}", _collectionName);
            }

            // Step 2: Ensure SessionId payload index exists
            await EnsurePayloadIndexAsync(
                "SessionId",
                PayloadSchemaType.Uuid,
                cancellationToken);

            // Step 3: Ensure Category payload index exists
            await EnsurePayloadIndexAsync(
                "Category",
                PayloadSchemaType.Keyword,
                cancellationToken);
        }

        private async Task EnsurePayloadIndexAsync(
            string fieldName,
            PayloadSchemaType schemaType,
            CancellationToken cancellationToken)
        {
            try
            {
                var collectionInfo = await _client.GetCollectionInfoAsync(_collectionName, cancellationToken);
                if (collectionInfo.PayloadSchema.ContainsKey(fieldName))
                {
                    _logger.LogInformation("{FieldName} index exists.", fieldName);
                    return;
                }

                await _client.CreatePayloadIndexAsync(
                    _collectionName,
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
            List<KnowledgeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            if (chunks == null || chunks.Count == 0) return;

            var texts = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken);

            var points = new List<PointStruct>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var embedding = embeddings[i];

                var payload = new Dictionary<string, Value>
                {
                    { "DocumentId", chunk.DocumentId.ToString() },
                    { "SessionId", chunk.SessionId.ToString() },
                    { "Category", ((int)chunk.Category).ToString() }, // store as int string
                    { "Content", chunk.Content },
                    { "ChunkIndex", chunk.ChunkIndex },
                    { "CreatedAt", chunk.CreatedAt.ToString("o") }
                };

                var point = new PointStruct
                {
                    Id = chunk.Id,
                    Vectors = embedding,
                    Payload = { payload }
                };
                points.Add(point);
            }

            await _client.UpsertAsync(_collectionName, points, cancellationToken: cancellationToken);
        }

        public async Task<List<KnowledgeChunk>> SearchAsync(
            Guid sessionId,
            string queryText,
            int topK = 5,
            DocumentCategory? categoryFilter = null,
            CancellationToken cancellationToken = default)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "SessionId is required and cannot be empty — unscoped search is not allowed.",
                    nameof(sessionId));
            }

            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(queryText, cancellationToken);

            var conditions = new List<Condition>
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "SessionId",
                        Match = new Match { Keyword = sessionId.ToString() }
                    }
                }
            };

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

            var searchResult = await _client.SearchAsync(
                _collectionName,
                queryEmbedding,
                filter: filter,
                limit: (ulong)topK,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Qdrant Search | Query: '{QueryText}' | TopK: {TopK} | Returned {Count} results", queryText, topK, searchResult.Count);

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
                    SessionId = Guid.Parse(payload["SessionId"].StringValue),
                    Category = (DocumentCategory)int.Parse(payload["Category"].StringValue),
                    Content = payload["Content"].StringValue,
                    ChunkIndex = chunkIndex,
                    CreatedAt = DateTime.Parse(payload["CreatedAt"].StringValue)
                };
                resultChunks.Add(chunk);
            }

            return resultChunks;
        }
    }
}
