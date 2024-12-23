using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace FunctionApp2
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private static readonly string FilePath = Path.Combine("Data", "users.json");
        //private static readonly string FilePath = Path.Combine(Directory.GetCurrentDirectory(), "users.json");
        public Function1(ILogger<Function1> log)
        {
            _logger = log;
        }

        [FunctionName("Function1")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
        [FunctionName("CreateUser")]
        [OpenApiOperation(operationId: "CreateUser", tags: new[] { "Users" })]
        [OpenApiRequestBody("application/json", typeof(User), Description = "User object to create")]
        [OpenApiResponseWithBody(HttpStatusCode.Created, "application/json", typeof(User), Description = "User created successfully")]
        public static async Task<IActionResult> CreateUser(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users")] HttpRequest req,
    ILogger log)
        {
            log.LogInformation("Processing POST request to create a user.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation("Request Body: {RequestBody}", requestBody);

            var newUser = JsonConvert.DeserializeObject<User>(requestBody);
            if (newUser == null || string.IsNullOrEmpty(newUser.Id))
            {
                log.LogWarning("Invalid user data.");
                return new BadRequestObjectResult("Invalid user data.");
            }

            var users = ReadUsers();
            if (users.Exists(u => u.Id == newUser.Id))
            {
                log.LogWarning("User with ID {UserId} already exists.", newUser.Id);
                return new ConflictObjectResult($"User with ID {newUser.Id} already exists.");
            }

            users.Add(newUser);
            SaveUsers(users);

            log.LogInformation("User with ID {UserId} created successfully.", newUser.Id);
            return new CreatedResult($"/users/{newUser.Id}", newUser);
        }
        [FunctionName("GetUsers")]
        [OpenApiOperation(operationId: "GetUsers", tags: new[] { "Users" })]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = false, Type = typeof(string), Description = "The user ID parameter.")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(User), Description = "The OK response")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "User not found")]
        public static IActionResult GetUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{id}")] HttpRequest req,
        ILogger log,
        string id)
        {
            log.LogInformation("Processing GET request.");

            var users = ReadUsers();

            if (!string.IsNullOrEmpty(id))
            {
                var user = users.Find(u => u.Id == id);
                return user != null ? new OkObjectResult(user) : new NotFoundObjectResult($"User with ID {id} not found.");
            }

            return new OkObjectResult(users);
        }
        [FunctionName("GetAllUsers")]
        [OpenApiOperation(operationId: "GetAllUsers", tags: new[] { "Users" })]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<User>), Description = "The list of users")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "User not found")]
        public static IActionResult GetAllUsers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users")] HttpRequest req, ILogger log)
        {
            log.LogInformation("Processing GET request for all users."); 
            var users = ReadUsers();
            return new OkObjectResult(users);
        }

        [FunctionName("UpdateUser")]
        [OpenApiOperation(operationId: "UpdateUser", tags: new[] { "Users" })]
        [OpenApiRequestBody("application/json", typeof(User), Description = "Updated user data")]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The user ID")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(User), Description = "The updated user")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid user data")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "User not found")]
        public static async Task<IActionResult> UpdateUser([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "users/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation("Processing PUT request.");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updatedUser = JsonConvert.DeserializeObject<User>(requestBody);
            if (updatedUser == null)
            {
                return new BadRequestObjectResult("Invalid user data.");
            }
            var users = ReadUsers(); 
            var userIndex = users.FindIndex(u => u.Id == id);
            if (userIndex == -1)
            {
                return new NotFoundObjectResult($"User with ID {id} not found.");
            }
            users[userIndex] = updatedUser;
            SaveUsers(users);
            return new OkObjectResult(updatedUser);
        }
        [FunctionName("DeleteUser")]
        [OpenApiOperation(operationId: "DeleteUser", tags: new[] { "Users" })]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The user ID")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "User deleted")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "User not found")]
        public static IActionResult DeleteUser([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "users/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation("Processing DELETE request.");
            var users = ReadUsers(); var userIndex = users.FindIndex(u => u.Id == id); if (userIndex == -1)
            {
                return new NotFoundObjectResult($"User with ID {id} not found.");
            }
            users.RemoveAt(userIndex);
            SaveUsers(users);
            return new OkObjectResult($"User with ID {id} deleted.");
        }
        private static List<User> ReadUsers()
        {
            if (!File.Exists(FilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? string.Empty);
                File.WriteAllText(FilePath, "[]");
            }
            //if (!System.IO.File.Exists(FilePath))
            //{                
            //    System.IO.File.WriteAllText(FilePath, "[]");                
            //}

            string json = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<List<User>>(json) ?? new List<User>();
        }
        private static void SaveUsers(List<User> users)
        {
            string json = JsonConvert.SerializeObject(users, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
    }
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
}

