using Buggy.API.Data;
using Buggy.API.DTOs;
using Buggy.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Controllers;

/// <summary>Display names of all users, e.g. to pick or validate an assignee.</summary>
[ApiController]
[Route("api/members")]
[Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
public class MembersController : ControllerBase
{
    private readonly BuggyDbContext _db;

    public MembersController(BuggyDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<MemberDto>>> GetAll() =>
        Ok(await _db.Users
            .OrderBy(u => u.DisplayName)
            .Select(u => new MemberDto(u.DisplayName))
            .ToListAsync());
}
