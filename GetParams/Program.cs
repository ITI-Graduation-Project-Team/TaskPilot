using System;
using System.Linq;
using Qdrant.Client;
using System.Reflection;

class Program {
    static void Main() {
        foreach (var m in typeof(QdrantClient).GetMethods()) {
            if (m.Name == "RetrieveAsync") {
                Console.WriteLine(m.ReturnType.Name);
                if (m.ReturnType.IsGenericType) {
                    Console.WriteLine("Generic args: " + string.Join(", ", m.ReturnType.GetGenericArguments().Select(a => a.Name)));
                }
            }
        }
    }
}
