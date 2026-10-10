using System.Xml.Linq;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverNativeVisualStateTests
{
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";
    private const string VisualStateProperty = "VisualStateManager.VisualStateGroups";

    [Fact]
    public void ProfileMenuButtons_HaveUniqueGroupsAndStatesAfterStyleInheritanceAndLocalValues()
    {
        var menu = XDocument.Load(RepoFile("FDriverApp/Controls/DriverProfileMenuRow.xaml"));
        var styles = Styles(XDocument.Load(RepoFile("FDriverApp/Resources/Styles/AppStyles.xaml")).Root!);
        var buttons = menu.Descendants(Maui + "Button").ToArray();
        Assert.NotEmpty(buttons);
        foreach (var button in buttons)
        {
            var issues = Duplicates(EffectiveGroups(button, styles));
            Assert.True(issues.Count == 0,
                $"{Name(button)} ({button.Attribute("Style")?.Value}): {string.Join(", ", issues)}. "
                + "Local implicit groups append to the inherited list; an explicit VisualStateGroupList replaces it.");
        }
    }

    [Fact]
    public void ImplicitLocalGroup_AppendsToGroupsInheritedThroughBasedOn_AndDetectsDuplicateNames()
    {
        var (button, styles) = Fixture();
        button.Add(new XElement(Maui + VisualStateProperty, Group("CommonStates", "Normal", "Pressed")));

        var groups = EffectiveGroups(button, styles);
        Assert.Equal(new[] { "CommonStates", "CommonStates" }, groups.Select(group => group.Name));
        Assert.Contains("group 'CommonStates'", Duplicates(groups));
        Assert.Contains("state 'Normal'", Duplicates(groups));
    }

    [Fact]
    public void ImplicitLocalGroup_WithDistinctNamesPreservesBothInheritedAndLocalStates()
    {
        var (button, styles) = Fixture();
        button.Add(new XElement(Maui + VisualStateProperty, Group("TaskStates", "TaskSelected")));

        var groups = EffectiveGroups(button, styles);
        Assert.Equal(new[] { "Normal", "Disabled", "TaskSelected" }, groups.SelectMany(group => group.States));
        Assert.Empty(Duplicates(groups));
    }

    [Fact]
    public void ExplicitLocalList_ReplacesInheritedGroupsInsteadOfAppendingToThem()
    {
        var (button, styles) = Fixture();
        button.Add(new XElement(Maui + VisualStateProperty,
            new XElement(Maui + "VisualStateGroupList", Group("CommonStates", "Normal", "Pressed", "Focused"))));

        var group = Assert.Single(EffectiveGroups(button, styles));
        Assert.Equal("CommonStates", group.Name);
        Assert.Equal(new[] { "Normal", "Pressed", "Focused" }, group.States);
        Assert.Empty(Duplicates([group]));
    }

    [Fact]
    public void DerivedStyleSetter_ReplacesBaseStyleList_BeforeLocalGroupsAreAdded()
    {
        var (button, styles) = Fixture();
        styles["Derived"].Add(StateSetter(Group("DerivedStates", "Focused")));
        button.Add(new XElement(Maui + VisualStateProperty, Group("TaskStates", "TaskSelected")));

        var groups = EffectiveGroups(button, styles);
        Assert.Equal(new[] { "DerivedStates", "TaskStates" }, groups.Select(group => group.Name));
        Assert.Equal(new[] { "Focused", "TaskSelected" }, groups.SelectMany(group => group.States));
        Assert.Empty(Duplicates(groups));
    }

    [Fact]
    public void ExplicitReplacement_StillRejectsRepeatedStateNamesAcrossDifferentGroups()
    {
        var (button, styles) = Fixture();
        button.Add(new XElement(Maui + VisualStateProperty, new XElement(Maui + "VisualStateGroupList",
            Group("FirstStates", "Normal"), Group("SecondStates", "Normal"))));

        Assert.Equal(new[] { "state 'Normal'" }, Duplicates(EffectiveGroups(button, styles)));
    }

    // This models only the Button + explicit StaticResource style boundary used by
    // the native menu. It does not load MAUI, execute bindings, or validate APK startup.
    private static IReadOnlyList<StateGroup> EffectiveGroups(XElement button, IReadOnlyDictionary<string, XElement> styles)
    {
        var inherited = StyleGroups(ResourceKey(button.Attribute("Style")!.Value), styles, new HashSet<string>(StringComparer.Ordinal));
        var local = button.Element(Maui + VisualStateProperty);
        if (local is null) return inherited;
        if (local.Element(Maui + "VisualStateGroupList") is { } replacement)
        {
            Assert.Single(local.Elements());
            return ReadGroups(replacement);
        }
        return inherited.Concat(ReadGroups(local)).ToArray();
    }

    private static IReadOnlyList<StateGroup> StyleGroups(string key, IReadOnlyDictionary<string, XElement> styles, HashSet<string> visited)
    {
        Assert.True(visited.Add(key), $"Cyclic Button style inheritance at '{key}'.");
        Assert.True(styles.TryGetValue(key, out var style), $"Button style '{key}' is missing.");
        Assert.Equal("Button", (string?)style!.Attribute("TargetType"));
        IReadOnlyList<StateGroup> inherited = style.Attribute("BasedOn") is { } basedOn
            ? StyleGroups(ResourceKey(basedOn.Value), styles, visited) : [];
        var setter = style.Elements(Maui + "Setter").LastOrDefault(element => (string?)element.Attribute("Property") == VisualStateProperty);
        return setter is null ? inherited : ReadGroups(Assert.Single(setter.Elements(Maui + "VisualStateGroupList")));
    }

    private static StateGroup[] ReadGroups(XElement collection)
        => collection.Elements(Maui + "VisualStateGroup")
            .Select(group => new StateGroup(Name(group), group.Elements(Maui + "VisualState").Select(Name).ToArray())).ToArray();

    private static List<string> Duplicates(IReadOnlyList<StateGroup> groups)
        => groups.GroupBy(group => group.Name, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => $"group '{group.Key}'")
            .Concat(groups.SelectMany(group => group.States).GroupBy(state => state, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => $"state '{group.Key}'")).ToList();

    private static string Name(XElement element)
        => (string?)element.Attribute(Xaml + "Name") ?? (string?)element.Attribute("Name")
            ?? throw new InvalidOperationException($"{element.Name.LocalName} requires a name for this Button state check.");

    private static string ResourceKey(string value)
    {
        const string prefix = "{StaticResource ";
        Assert.StartsWith(prefix, value); Assert.EndsWith("}", value);
        return value[prefix.Length..^1].Trim();
    }

    private static Dictionary<string, XElement> Styles(XElement dictionary)
        => dictionary.Elements(Maui + "Style").Where(style => style.Attribute(Xaml + "Key") is not null)
            .ToDictionary(style => style.Attribute(Xaml + "Key")!.Value, StringComparer.Ordinal);

    private static (XElement Button, Dictionary<string, XElement> Styles) Fixture()
    {
        var dictionary = new XElement(Maui + "ResourceDictionary",
            new XElement(Maui + "Style", new XAttribute(Xaml + "Key", "Base"), new XAttribute("TargetType", "Button"),
                StateSetter(Group("CommonStates", "Normal", "Disabled"))),
            new XElement(Maui + "Style", new XAttribute(Xaml + "Key", "Derived"), new XAttribute("TargetType", "Button"),
                new XAttribute("BasedOn", "{StaticResource Base}")));
        return (new XElement(Maui + "Button", new XAttribute("Style", "{StaticResource Derived}")), Styles(dictionary));
    }

    private static XElement StateSetter(params XElement[] groups)
        => new(Maui + "Setter", new XAttribute("Property", VisualStateProperty), new XElement(Maui + "VisualStateGroupList", groups));

    private static XElement Group(string name, params string[] states)
        => new(Maui + "VisualStateGroup", new XAttribute(Xaml + "Name", name),
            states.Select(state => new XElement(Maui + "VisualState", new XAttribute(Xaml + "Name", state))));

    private static string RepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.v3.5.slnx")))
                return Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        throw new InvalidOperationException("Repository root was not found for native Button visual-state checks.");
    }

    private sealed record StateGroup(string Name, IReadOnlyList<string> States);
}
