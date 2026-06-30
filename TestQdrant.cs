using System;
using System.Threading.Tasks;
using Qdrant.Client;

namespace TestQdrant
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new QdrantClient(host: "1723e0a6-08cc-46d5-8657-a14f25431eda.eu-central-1-0.aws.cloud.qdrant.io", port: 6334, https: true, apiKey: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhY2Nlc3MiOiJtIiwic3ViamVjdCI6ImFwaS1rZXk6YWZkZTUyMTEtZDg3OS00OWI5LTkyNmItNGJiODNlYzM4NGNiIn0.8p8SPg2RKsEgGaI3E9keO2dVlCHT4sRM3aR4my_6EIY");
            var info = await client.GetCollectionInfoAsync("taskpilot_knowledge");
            Console.WriteLine(info.PayloadSchema.Count);
            foreach (var kvp in info.PayloadSchema)
            {
                Console.WriteLine($"{kvp.Key}");
            }
        }
    }
}
