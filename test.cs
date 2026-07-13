using System;
using Qdrant.Client.Grpc;

class Program
{
    static void Main()
    {
        var m = new Match { Value = "test" };
        var m2 = new Match { Keyword = "test" };
        Console.WriteLine("Value: " + (m.Value != null));
        Console.WriteLine("Keyword: " + m2.Keyword);
    }
}
