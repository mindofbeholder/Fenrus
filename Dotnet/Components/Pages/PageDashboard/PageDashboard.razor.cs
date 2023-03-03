using Fenrus.Components.Dialogs;
using Fenrus.Models;
using Fenrus.Models.UiModels;
using Fenrus.Pages;
using Fenrus.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fenrus.Components;

/// <summary>
/// Page Dashboard
/// </summary>
public partial class PageDashboard : CommonPage<Models.Group>
{
    [Inject] private NavigationManager Router { get; set; }
    Models.Dashboard Model { get; set; } = new();

    /// <summary>
    /// Gets if the dashboard being edited is the guest dashboard
    /// </summary>
    private bool IsGuest => Uid == Globals.GuestDashbardUid;

    /// <summary>
    /// The groups in this dashboard
    /// </summary>
    private List<Models.Group> Groups = new();

    private FenrusTable<Models.Group> Table { get; set; }

    private GroupAddDialog GroupAddDialog { get; set; }

    [Parameter]
    public string UidString
    {
        get => Uid.ToString();
        set
        {
            if (Guid.TryParse(value, out Guid guid))
                this.Uid = guid;
            else
            {
                // do a redirect
            }
        }
    }

    /// <summary>
    /// Gets or sets the UID of the dashboard
    /// </summary>
    public Guid Uid { get; set; }
    private bool isNew = false;

    private string lblTitle, lblNameHelp, lblShowSearch, lblShowGroupTitles, lblLinkTarget,
        lblOpenInThisTab,lblOpenInNewTab, lblOpenInSameTab, lblAccentColor, lblBackgroundColor,
        lblTheme;

    private List<ListOption> Themes;

    protected override async Task PostGotUser()
    {
        lblTitle = Translater.Instant(IsGuest
            ? "Pages.Dashboard.Title-Guest"
            : "Pages.Dashboard.Title");
        lblNameHelp = Translater.Instant("Pages.Dashboard.Fields.Name-Help");
        lblShowSearch = Translater.Instant("Pages.Dashboard.Fields.ShowSearch");
        lblShowGroupTitles = Translater.Instant("Pages.Dashboard.Fields.ShowGroupTitles");
        lblTheme = Translater.Instant("Pages.Dashboard.Fields.Theme");
        lblAccentColor = Translater.Instant("Pages.Dashboard.Fields.AccentColor");
        lblBackgroundColor = Translater.Instant("Pages.Dashboard.Fields.BackgroundColor");
        lblLinkTarget = Translater.Instant("Pages.Dashboard.Fields.LinkTarget");
        lblOpenInThisTab = Translater.Instant("Enums.LinkTarget.OpenInThisTab");
        lblOpenInNewTab = Translater.Instant("Enums.LinkTarget.OpenInNewTab");
        lblOpenInSameTab = Translater.Instant("Enums.LinkTarget.OpenInSameTab");

        Themes = new ThemeService().GetThemes().Select(x => new ListOption()
        {
            Label = x,
            Value = x
        }).ToList();
        
        if (Uid == Guid.Empty)
        {
            // new item
            isNew = true;
            Model = new();
            Model.Name = "New Dashboard";
            var usedNames = Settings.Dashboards.Select(x => x.Name).ToArray();
            int count = 1;
            while (usedNames.Contains(Model.Name))
            {
                ++count;
                Model.Name = $"New Dashboard ({count})";
            }

            Model.AccentColor = "#ff0090";;
            Model.Theme = "Default";
        }
        else if (IsGuest)
        {
            isNew = false;
            Model = new DashboardService().GetGuestDashboard();
        }
        else
        {
            isNew = false;
            Model = Settings.Dashboards.FirstOrDefault(x => x.Uid == Uid);
            if (Model == null)
            {
                Router.NavigateTo("/settings/dashboards");
            }
        }
        var sysGroups = new GroupService().GetSystemGroups(enabledOnly: true);
        Groups = Settings.Groups.Where(x => Model.GroupUids.Contains(x.Uid))
            .Union(sysGroups.Where(x => Model.GroupUids.Contains(x.Uid))).ToList();
    }

    /// <summary>
    /// Gets all the groups that are available to this dashboard
    /// </summary>
    /// <param name="notInUse">When true, will filter out any group that is currently in this dashboard,
    /// otherwise all groups will be returned</param>
    /// <returns> all the groups that are available to this dashboard</returns>
    List<Models.Group> GetAllAvailableGroups(bool notInUse = false)
    {
        var userGroups = Settings.Groups;
        var sysGroups = new GroupService().GetSystemGroups(enabledOnly: true);

        var result = userGroups.Union(sysGroups);
        if (notInUse)
        {
            var used = Groups.Select(x => x.Uid);
            result = result.Where(x => used.Contains(x.Uid) == false);
        }
        return result.ToList();
    }
    
    void Save()
    {
        if (IsGuest)
        {
            var service = new DashboardService();
            var existing = service.GetGuestDashboard();
            existing.Enabled = Model.Enabled;
            existing.ShowSearch = Model.ShowSearch;
            existing.Theme = Model.Theme;
            existing.LinkTarget = Model.LinkTarget;
            existing.AccentColor = Model.AccentColor;
            existing.BackgroundColor = Model.BackgroundColor;
            existing.GroupUids = Groups?.Select(x => x.Uid)?.ToList() ?? new ();
            service.Update(existing);
            ToastService.ShowSuccess(Translater.Instant("Labels.Saved"));
            return;
        }
        
        if (isNew)
        {
            Model.Uid = Guid.NewGuid();
            Model.GroupUids = Groups.Select(x => x.Uid).ToList();
            Settings.Dashboards.Add(Model);
        } 
        else
        {
            var existing = Settings.Dashboards.First(x => x.Uid == Uid);
            existing.Name = Model.Name;
            existing.GroupUids = Groups?.Select(x => x.Uid)?.ToList() ?? new ();
        }
        Settings.Save();
        this.Router.NavigateTo("/settings/dashboards");
    }

    void Cancel()
    {
        this.Router.NavigateTo("/settings/dashboards");
    }

    async Task AddGroup()
    {
        var available = GetAllAvailableGroups(notInUse: true);
        if (available.Any() == false)
        {
            ToastService.ShowWarning(Translater.Instant("Pages.Dashboard.Messages.NoAvailableGroups"));
            return;
        }
        var adding = await GroupAddDialog.Show(available.Select(x => new ListOption
        {
            Label = x.Name, 
            Value = x.Uid
        }).ToList());

        if (adding?.Any() != true)
            return;
        foreach (var opt in adding)
        {
            if (opt.Value is Guid uid == false)
                continue;
            if (Groups.Any(x => x.Uid == uid))
                continue;
            var group = available.FirstOrDefault(x => x.Uid == uid);
            if (group == null)
                continue;
            Groups.Add(group);
        }
        Table.SetData(Groups);
    }

    protected override bool DoDelete(Models.Group item)
    {
        if (Groups.Contains(item) == false)
            return false;
        Groups.Remove(item);
        Table.SetData(Groups);
        return true;
    }
}