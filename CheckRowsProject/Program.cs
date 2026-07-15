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
                    Console.WriteLine("--- Users ---");
                    string queryUsers = "SELECT Id, Email, UserName, UserType FROM AspNetUsers WHERE Email IN ('macavo3608@duvips.com', 'ridat53647@bevriz.com')";
                    using (SqlCommand cmd = new SqlCommand(queryUsers, connection))
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            Console.WriteLine($"User: {r["Id"]}, Email: {r["Email"]}, Username: {r["UserName"]}, UserType: {r["UserType"]}");
                    }

                    Console.WriteLine("\n--- Projects ---");
                    string queryProjects = "SELECT Id, NameEn, Status, ManagerId FROM Projects WHERE NameEn LIKE '%Hotel%'";
                    using (SqlCommand cmd = new SqlCommand(queryProjects, connection))
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            Console.WriteLine($"Project: {r["Id"]}, Name: {r["NameEn"]}, Status: {r["Status"]}, ManagerId: {r["ManagerId"]}");
                    }

                    Console.WriteLine("\n--- Tasks & User Stories for Employee ---");
                    string queryTasks = @"
                        SELECT t.Id AS TaskId, t.TitleEn AS TaskTitle, t.SprintId AS TaskSprintId, t.Status AS TaskStatus,
                               s.Id AS StoryId, s.TitleEn AS StoryTitle, s.SprintId AS StorySprintId,
                               p.NameEn AS ProjectName
                        FROM Tasks t
                        JOIN UserStories s ON t.UserStoryId = s.Id
                        JOIN Projects p ON s.ProjectId = p.Id
                        JOIN AspNetUsers u ON t.EmployeeId = u.Id
                        WHERE u.Email = 'macavo3608@duvips.com'";
                    using (SqlCommand cmd = new SqlCommand(queryTasks, connection))
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var taskSprint = r["TaskSprintId"] == DBNull.Value ? "NULL" : r["TaskSprintId"].ToString();
                            var storySprint = r["StorySprintId"] == DBNull.Value ? "NULL" : r["StorySprintId"].ToString();
                            Console.WriteLine($"Project: {r["ProjectName"]}\n" +
                                              $"  Story: [{r["StoryId"]}] {r["StoryTitle"]} (SprintId: {storySprint})\n" +
                                              $"  Task: [{r["TaskId"]}] {r["TaskTitle"]} (SprintId: {taskSprint}, Status: {r["TaskStatus"]})");
                        }
                    }

                    Console.WriteLine("\n--- Project's Sprints ---");
                    string querySprints = @"
                        SELECT s.Id, s.TitleEn, s.Status, s.ProjectId
                        FROM Sprints s
                        JOIN Projects p ON s.ProjectId = p.Id
                        WHERE p.NameEn LIKE '%Hotel%'";
                    using (SqlCommand cmd = new SqlCommand(querySprints, connection))
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            Console.WriteLine($"Sprint: {r["Id"]}, Title: {r["TitleEn"]}, Status: {r["Status"]}, ProjectId: {r["ProjectId"]}");
                        }
                    }

                } catch (Exception ex) {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }
    }
}
