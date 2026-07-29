using System;
using System.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connStr = "Server=db49999.public.databaseasp.net; Database=db49999; User Id=db49999; Password=Qj6#+Xw2o9W=; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TaskAiSummaries'", conn);
            using (var reader = cmd.ExecuteReader())
            {
                int count = 0;
                while (reader.Read()) count++;
                Console.WriteLine("Rows found: " + count);
            }
        }
    }
}
