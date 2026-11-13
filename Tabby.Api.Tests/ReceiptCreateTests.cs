using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tabby.Api.Controllers;
using Tabby.Api.Data;
using Tabby.Api.DTOs;
using Tabby.Api.Models;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Tabby.Api.Hubs;

namespace Tabby.Api.Tests;

public class ReceiptCreateTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    // xUnit builds a new instance of this class for every test,
    // so each test starts with its own empty database
    public ReceiptCreateTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();   // the in-memory database lives only while this stays open

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ---------- helpers ----------

    private User AddUser(string name)
    {
        var user = new User
        {
            Username = name,
            Email = $"{name}@test.com",
            Tag = "0001",
            PasswordHash = "not-a-real-hash"
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private void AddFriendship(User requester, User addressee, FriendshipStatus status)
    {
        _db.Friendships.Add(new Friendship
        {
            RequesterId = requester.Id,
            AddresseeId = addressee.Id,
            Status = status
        });
        _db.SaveChanges();
    }

    // A controller that believes `user` is logged in, as if their JWT had been validated.
    // The hub is null because Create never touches it (only SetPaid does).
    // A controller that believes `user` is logged in, as if their JWT had been validated.
    // The hub is a stand-in that accepts every push and does nothing with it.
    private ReceiptsController ControllerFor(User user)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) },
            authenticationType: "test");

        return new ReceiptsController(_db, Substitute.For<IHubContext<ChatHub>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static CreateReceiptRequest RequestWith(params int[] friendIds) => new()
    {
        Title = "Groceries",
        TotalAmount = 10.00m,
        Shares = friendIds.Select(id => new ShareInput { UserId = id }).ToList()
    };

    // ---------- tests ----------

    [Fact]
    public async Task Create_WithAcceptedFriend_Succeeds()
    {
        var anna = AddUser("anna");
        var boris = AddUser("boris");
        AddFriendship(anna, boris, FriendshipStatus.Accepted);

        var result = await ControllerFor(anna).Create(RequestWith(boris.Id));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var receipt = Assert.IsType<ReceiptDto>(created.Value);
        Assert.Equal(2, receipt.Shares.Count);                      // boris + the payer
        Assert.Equal(10.00m, receipt.Shares.Sum(s => s.Amount));    // nothing lost
    }

    [Fact]
    public async Task Create_WithFriendWhoSentTheRequest_Succeeds()
    {
        // A friendship is stored once, so the direction must not matter
        var anna = AddUser("anna");
        var boris = AddUser("boris");
        AddFriendship(boris, anna, FriendshipStatus.Accepted);

        var result = await ControllerFor(anna).Create(RequestWith(boris.Id));

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Create_WithStranger_IsRejected()
    {
        var anna = AddUser("anna");
        var stranger = AddUser("stranger");

        var result = await ControllerFor(anna).Create(RequestWith(stranger.Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(_db.Receipts);   // and nothing was saved
    }

    [Fact]
    public async Task Create_WithPendingRequest_IsRejected()
    {
        var anna = AddUser("anna");
        var boris = AddUser("boris");
        AddFriendship(anna, boris, FriendshipStatus.Pending);   // not accepted yet

        var result = await ControllerFor(anna).Create(RequestWith(boris.Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(_db.Receipts);
    }

    [Fact]
    public async Task Create_IncludingYourself_IsRejected()
    {
        var anna = AddUser("anna");

        var result = await ControllerFor(anna).Create(RequestWith(anna.Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_MarksThePayersOwnShareAsPaid()
    {
        var anna = AddUser("anna");
        var boris = AddUser("boris");
        AddFriendship(anna, boris, FriendshipStatus.Accepted);

        var result = await ControllerFor(anna).Create(RequestWith(boris.Id));

        var receipt = Assert.IsType<ReceiptDto>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.True(receipt.Shares.Single(s => s.User.UserId == anna.Id).IsPaid);
        Assert.False(receipt.Shares.Single(s => s.User.UserId == boris.Id).IsPaid);
    }

    [Fact]
    public async Task Create_WithoutMyShare_FriendsSplitTheWholeTotal()
    {
        var anna = AddUser("anna");
        var boris = AddUser("boris");
        AddFriendship(anna, boris, FriendshipStatus.Accepted);

        var request = RequestWith(boris.Id);
        request.IncludeMyShare = false;

        var result = await ControllerFor(anna).Create(request);

        var receipt = Assert.IsType<ReceiptDto>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        var share = Assert.Single(receipt.Shares);      // only boris, no share for anna
        Assert.Equal(boris.Id, share.User.UserId);
        Assert.Equal(10.00m, share.Amount);             // the whole total
    }

    [Fact]
    public async Task Create_NotifiesEachPersonAdded_ButNotTheCreator()
    {
        var anna = AddUser("anna");
        var boris = AddUser("boris");
        AddFriendship(anna, boris, FriendshipStatus.Accepted);

        await ControllerFor(anna).Create(RequestWith(boris.Id));

        var notification = Assert.Single(_db.Notifications);   // one, and none for anna
        Assert.Equal(boris.Id, notification.RecipientId);
        Assert.Equal(anna.Id, notification.ActorId);
        Assert.Equal(NotificationType.AddedToReceipt, notification.Type);
    }
}