using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        List<(string EmployeeId, string ReasonEn, string ReasonAr)> reasons = new();
        
        // Simulating FirstOrDefault when no match is found
        var devReason = reasons.FirstOrDefault(r => r.EmployeeId == "non-existent-id");
        
        Console.WriteLine($"devReason.EmployeeId is null: {devReason.EmployeeId == null}");
        Console.WriteLine($"devReason.ReasonEn is null: {devReason.ReasonEn == null}");
        
        string ReasonEn = !string.IsNullOrEmpty(devReason.ReasonEn) ? devReason.ReasonEn : "Fallback En";
        string ReasonAr = !string.IsNullOrEmpty(devReason.ReasonAr) ? devReason.ReasonAr : "Fallback Ar";
        
        Console.WriteLine($"ReasonEn: {ReasonEn}");
        Console.WriteLine($"ReasonAr: {ReasonAr}");
    }
}
