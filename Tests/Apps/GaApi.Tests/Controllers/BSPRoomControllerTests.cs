namespace GaApi.Tests.Controllers;

using GaApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using Moq;

[TestFixture]
public class BspRoomControllerTests
{
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<BspRoomController>>();
        _controller = new BspRoomController(_mockLogger.Object);
    }

    private BspRoomController _controller = null!;
    private Mock<ILogger<BspRoomController>> _mockLogger = null!;

    [Test]
    public void GenerateDungeon_WithDefaultParams_ShouldReturnSuccess()
    {
        // Arrange
        var @params = new DungeonGenerationParams();

        // Act
        var result = _controller.GenerateDungeon(@params);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.InstanceOf<ApiResponse<DungeonLayoutResponse>>());

        var response = (ApiResponse<DungeonLayoutResponse>)okResult.Value!;
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data!.Rooms, Is.Not.Empty);
        Assert.That(response.Data.Corridors, Is.Not.Empty);
    }

    [Test]
    public void GenerateDungeon_WithSeed_ShouldBeReproducible()
    {
        // Arrange
        var seed = 42;
        var params1 = new DungeonGenerationParams { Seed = seed };
        var params2 = new DungeonGenerationParams { Seed = seed };

        // Act
        var result1 = _controller.GenerateDungeon(params1);
        var result2 = _controller.GenerateDungeon(params2);

        // Assert
        var okResult1 = (OkObjectResult)result1;
        var okResult2 = (OkObjectResult)result2;

        var response1 = (ApiResponse<DungeonLayoutResponse>)okResult1.Value!;
        var response2 = (ApiResponse<DungeonLayoutResponse>)okResult2.Value!;

        Assert.That(response1.Data!.Rooms.Count, Is.EqualTo(response2.Data!.Rooms.Count));

        // Check that first room is identical
        var room1 = response1.Data.Rooms[0];
        var room2 = response2.Data.Rooms[0];

        Assert.That(room1.X, Is.EqualTo(room2.X));
        Assert.That(room1.Y, Is.EqualTo(room2.Y));
        Assert.That(room1.Width, Is.EqualTo(room2.Width));
        Assert.That(room1.Height, Is.EqualTo(room2.Height));
    }

    [Test]
    public void GenerateDungeon_WithCustomSize_ShouldRespectDimensions()
    {
        // Arrange
        var @params = new DungeonGenerationParams
        {
            Width = 100,
            Height = 80
        };

        // Act
        var result = _controller.GenerateDungeon(@params);

        // Assert
        var okResult = (OkObjectResult)result;
        var response = (ApiResponse<DungeonLayoutResponse>)okResult.Value!;

        Assert.That(response.Data!.Width, Is.EqualTo(100));
        Assert.That(response.Data.Height, Is.EqualTo(80));

        // All rooms should be within bounds
        foreach (var room in response.Data.Rooms)
        {
            Assert.That(room.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(room.Y, Is.GreaterThanOrEqualTo(0));
            Assert.That(room.X + room.Width, Is.LessThanOrEqualTo(100));
            Assert.That(room.Y + room.Height, Is.LessThanOrEqualTo(80));
        }
    }

    [Test]
    public void GenerateDungeonDefault_ShouldReturnSuccess()
    {
        // Act
        var result = _controller.GenerateDungeonDefault();

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        var response = (ApiResponse<DungeonLayoutResponse>)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
    }

    [Test]
    public void GetInfo_ShouldReturnAlgorithmInfo()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        var response = (ApiResponse<object>)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
    }

    [Test]
    public void GenerateBatch_WithValidCount_ShouldReturnMultipleDungeons()
    {
        // Arrange
        var count = 3;

        // Act
        var result = _controller.GenerateBatch(count);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        var response = (ApiResponse<List<DungeonLayoutResponse>>)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data!.Count, Is.EqualTo(count));

        // Each dungeon should be different (different seeds)
        Assert.That(response.Data[0].Seed, Is.Not.EqualTo(response.Data[1].Seed));
    }

    [Test]
    public void GenerateBatch_WithInvalidCount_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidCount = 15; // Max is 10

        // Act
        var result = _controller.GenerateBatch(invalidCount);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public void GenerateDungeon_RoomsShouldHaveValidCenters()
    {
        // Arrange
        var @params = new DungeonGenerationParams();

        // Act
        var result = _controller.GenerateDungeon(@params);

        // Assert
        var okResult = (OkObjectResult)result;
        var response = (ApiResponse<DungeonLayoutResponse>)okResult.Value!;

        foreach (var room in response.Data!.Rooms)
        {
            var expectedCenterX = room.X + room.Width / 2;
            var expectedCenterY = room.Y + room.Height / 2;

            Assert.That(room.CenterX, Is.EqualTo(expectedCenterX));
            Assert.That(room.CenterY, Is.EqualTo(expectedCenterY));
        }
    }

    [Test]
    public void GenerateDungeon_CorridorsShouldConnectRooms()
    {
        // Arrange
        var @params = new DungeonGenerationParams();

        // Act
        var result = _controller.GenerateDungeon(@params);

        // Assert
        var okResult = (OkObjectResult)result;
        var response = (ApiResponse<DungeonLayoutResponse>)okResult.Value!;

        // Should have corridors connecting rooms
        Assert.That(response.Data!.Corridors.Count, Is.GreaterThan(0));

        // Each corridor should have at least 2 points
        foreach (var corridor in response.Data.Corridors)
        {
            Assert.That(corridor.Points.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(corridor.Width, Is.GreaterThan(0));
        }
    }
}
