using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL.Middlewares;
using StoresPOS.PL.ViewModels;

using StoresPOS.PL.Extensions;

namespace StoresPOS.PL.Controllers;

/// <summary>
/// Users and roles (Phase 21): searchable list, tabbed user page (details · role and permissions ·
/// security · activity), roles × permissions matrix, role templates. Changes apply without signing
/// in again (permission stamp, see <see cref="UserStampMiddleware"/>).
/// </summary>
[Authorize(Policy = AppPermissions.UsersManage)]
public class UsersController : Controller
{
    private static readonly string[] Tabs = ["details", "permissions", "security", "activity"];

    private readonly IUserAdminService _users;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly UserStampCache _stamps;

    public UsersController(IUserAdminService users, IStringLocalizer<SharedResource> localizer, UserStampCache stamps)
    {
        _users = users;
        _l = localizer;
        _stamps = stamps;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, int? roleId, string? status, CancellationToken cancellationToken)
    {
        SetHeader(_l["Users and roles"].Value, _l["Manage users and which tabs each person can see."].Value);
        return View(new UsersIndexViewModel
        {
            Users = await _users.ListAsync(new UserListFilterDto
            {
                Search = q,
                RoleId = roleId,
                IsActive = status switch { "active" => true, "inactive" => false, _ => null }
            }, cancellationToken),
            Roles = await _users.ListRolesAsync(cancellationToken),
            Search = q,
            RoleId = roleId,
            Status = status
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        SetHeader(_l["New user"].Value);
        return View("Edit", await BuildEditAsync(null, "details", cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, string? tab, CancellationToken cancellationToken)
    {
        var details = await _users.GetAsync(id, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        SetHeader(details.DisplayName);
        return View(await BuildEditAsync(details, tab, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(UserEditViewModel form, CancellationToken cancellationToken)
    {
        var dto = new UserUpsertDto
        {
            Username = form.Username,
            DisplayName = form.DisplayName,
            Password = form.Password,
            MustChangePassword = form.MustChangePassword,
            RoleId = form.RoleId,
            IsActive = form.IsActive,
            UseCustomTabs = form.UseCustomTabs,
            CustomPermissions = form.SelectedPermissions ?? []
        };

        try
        {
            var actorId = this.GetRequiredUserId();
            int id;
            if (form.Id is null)
            {
                id = await _users.CreateAsync(actorId, dto, cancellationToken);
            }
            else
            {
                id = form.Id.Value;
                await _users.UpdateAsync(actorId, id, dto, cancellationToken);
            }

            _stamps.Invalidate(id);
            TempData["Success"] = _l["User saved. Changes apply within a minute, no new sign-in needed."].Value;
            return RedirectToAction(nameof(Edit), new { id, tab = Tabs.Contains(form.Tab) ? form.Tab : "details" });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            var details = form.Id is null ? null : await _users.GetAsync(form.Id.Value, cancellationToken);
            var page = await BuildEditAsync(details, form.Tab, cancellationToken);
            // Keep what was typed.
            page.Username = form.Username;
            page.DisplayName = form.DisplayName;
            page.RoleId = form.RoleId;
            page.IsActive = form.IsActive;
            page.UseCustomTabs = form.UseCustomTabs;
            page.SelectedPermissions = form.SelectedPermissions ?? [];
            page.MustChangePassword = form.MustChangePassword;
            SetHeader(form.Id is null ? _l["New user"].Value : form.DisplayName);
            return View("Edit", page);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SetActive(int id, bool active, string? returnTo, CancellationToken cancellationToken) =>
        RunAsync(id, () => _users.SetActiveAsync(this.GetRequiredUserId(), id, active, cancellationToken),
            active ? _l["User activated."].Value : _l["User deactivated and signed out."].Value, returnTo, "details");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SignOutEverywhere(int id, string? returnTo, CancellationToken cancellationToken) =>
        RunAsync(id, () => _users.SignOutEverywhereAsync(this.GetRequiredUserId(), id, cancellationToken),
            _l["Signed out on every device."].Value, returnTo, "security");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResetPassword(int id, string? newPassword, bool mustChange, CancellationToken cancellationToken) =>
        RunAsync(id, () => _users.ResetPasswordAsync(this.GetRequiredUserId(), id, newPassword ?? string.Empty, mustChange, cancellationToken),
            _l["Password reset. The user was signed out."].Value, null, "security");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SetMustChange(int id, bool mustChange, CancellationToken cancellationToken) =>
        RunAsync(id, () => _users.SetMustChangePasswordAsync(this.GetRequiredUserId(), id, mustChange, cancellationToken),
            _l["Saved."].Value, null, "security");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unlock(int id, CancellationToken cancellationToken) =>
        RunAsync(id, () => _users.UnlockAsync(this.GetRequiredUserId(), id, cancellationToken),
            _l["Account unlocked."].Value, null, "security");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SetPin(int id, string? pin, bool remove, CancellationToken cancellationToken) =>
        RunAsync(id, () => _users.SetApprovalPinAsync(this.GetRequiredUserId(), id, remove ? null : pin, cancellationToken),
            remove ? _l["Approval PIN removed."].Value : _l["Approval PIN saved."].Value, null, "security");

    /// <summary>Roles × permissions matrix with templates.</summary>
    [HttpGet]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        SetHeader(_l["Role defaults"].Value, _l["Compare and edit what each role can do."].Value);
        return View(new RoleMatrixPageViewModel { Roles = await _users.GetRoleMatricesAsync(cancellationToken) });
    }

    /// <summary>Saves the whole matrix. Form fields: perm-{roleId} = permission keys.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRoles(List<int>? roleIds, CancellationToken cancellationToken)
    {
        try
        {
            var matrix = (roleIds ?? [])
                .Distinct()
                .ToDictionary(id => id, id => (IReadOnlyList<string>)Request.Form[$"perm-{id}"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToList());
            await _users.SaveRoleMatrixAsync(this.GetRequiredUserId(), matrix, cancellationToken);
            _stamps.Clear();
            TempData["Success"] = _l["Role defaults saved. Changes apply within a minute, no new sign-in needed."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Roles));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(string template, string name, CancellationToken cancellationToken)
    {
        try
        {
            await _users.CreateRoleFromTemplateAsync(this.GetRequiredUserId(), template, name, cancellationToken);
            TempData["Success"] = _l["Role created. Adjust its permissions below."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Roles));
    }

    private async Task<IActionResult> RunAsync(int id, Func<Task> action, string success, string? returnTo, string tab)
    {
        try
        {
            await action();
            _stamps.Invalidate(id);
            TempData["Success"] = success;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return returnTo == "list"
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Edit), new { id, tab });
    }

    private void SetHeader(string title, string? subtitle = null)
    {
        ViewData["Section"] = _l["Users and roles"].Value;
        ViewData["Title"] = title;
        if (subtitle is not null)
        {
            ViewData["Subtitle"] = subtitle;
        }
    }

    private async Task<UserEditViewModel> BuildEditAsync(UserDetailsDto? details, string? tab, CancellationToken cancellationToken)
    {
        var roles = await _users.ListRolesAsync(cancellationToken);
        var matrices = await _users.GetRoleMatricesAsync(cancellationToken);
        var rolePerms = matrices.ToDictionary(m => m.RoleId, m => m.Permissions);
        var safeTab = tab is not null && Tabs.Contains(tab) ? tab : "details";

        if (details is null)
        {
            var cashier = roles.FirstOrDefault(x => x.Name == AppRoles.Cashier) ?? roles.LastOrDefault();
            return new UserEditViewModel
            {
                Tab = safeTab is "security" or "activity" ? "details" : safeTab,
                Roles = roles,
                RoleId = cashier?.Id ?? 0,
                IsActive = true,
                MustChangePassword = true,
                RolePermissions = rolePerms,
                SelectedPermissions = (cashier is null ? [] : rolePerms.GetValueOrDefault(cashier.Id) ?? []).ToList()
            };
        }

        return new UserEditViewModel
        {
            Id = details.Id,
            Tab = safeTab,
            Username = details.Username,
            DisplayName = details.DisplayName,
            RoleId = details.RoleId,
            RoleName = details.RoleName,
            IsActive = details.IsActive,
            UseCustomTabs = details.UseCustomTabs,
            SelectedPermissions = details.EffectivePermissions.ToList(),
            Roles = roles,
            RolePermissions = rolePerms,
            Details = details,
            Activity = safeTab == "activity" ? await _users.GetActivityAsync(details.Id, cancellationToken) : [],
            IsSelf = details.Id == this.GetRequiredUserId()
        };
    }
}
