using System;
using System.Reflection;
using Qdrant.Client;
class Program {
    static void Main() {
        foreach (var m in typeof(QdrantClient).GetMethods()) {
            if (m.Name.Contains("SetPayload")) {
                Console.WriteLine($"{m.Name}(");
                foreach (var p in m.GetParameters()) Console.WriteLine($"  {p.ParameterType.Name} {p.Name}");
                Console.WriteLine(")");
            }
        }
    }
}
