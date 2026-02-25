using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Dtos;

namespace Api.Tests;

public class OwnershipTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public OwnershipTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetJob_OtherUsersJob_Returns403()
    {
        var ownerToken = TestWebAppFactory.TokenForUser(TestWebAppFactory.TestUserId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var createRequest = new CreateJobRequest(
            "Test Job",
            "BlazorServer",
            new List<FileInput> { new("MainWindow.xaml", "<Window></Window>") }
        );

        var createResponse = await _client.PostAsJsonAsync("/api/jobs", createRequest);
        var job = await createResponse.Content.ReadFromJsonAsync<JobResponse>();

        var otherToken = TestWebAppFactory.TokenForUser(TestWebAppFactory.OtherUserId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await _client.GetAsync($"/api/jobs/{job!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
