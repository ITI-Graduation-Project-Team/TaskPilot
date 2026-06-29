using System;
using System.Data.SqlClient;

namespace CheckRows
{
    class Program
    {
        static void Main(string[] args)
        {
            string connectionString = "Server=db49999.public.databaseasp.net; Database=db49999; User Id=db49999; Password=Qj6#+Xw2o9W=; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try {
                    connection.Open();
                    string query = "SELECT TOP 5 Id, NameEn, TechStack, PlatformTargets, ProjectType FROM Projects";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read()) {
                                Console.WriteLine($"Id: {reader["Id"]}, Name: {reader["NameEn"]}, TechStack: {reader["TechStack"]}, Platforms: {reader["PlatformTargets"]}, Type: {reader["ProjectType"]}");
                            }
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }
    }
}
