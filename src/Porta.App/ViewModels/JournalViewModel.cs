using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Porta.Models;
using Porta.Repositories;
using Porta.Services;

namespace Porta.ViewModels;

/// <summary>Journal: thesis cards filtered by emotion tag and asset, plan (TP/SL) vs. realisation (FR-11, US-04).</summary>
public sealed partial class JournalViewModel(
    IJournalRepository journals,
    PortfolioService portfolio,
    NotificationService notifications,
    SessionService session) : PageViewModelBase(notifications, session)
{
    private bool _loadingFilters;

    public override string Title => "Jurnal Investasi";

    public ObservableCollection<JournalViewDto> Entries { get; } = [];
    public ObservableCollection<FilterOption<int?>> AssetOptions { get; } = [];

    public IReadOnlyList<FilterOption<string>> EmotionOptions { get; } =
        [new("Semua Tag Emosi", null), .. EmotionTags.All.Select(t => new FilterOption<string>(t, t))];

    [ObservableProperty] private FilterOption<string>? _selectedEmotion;
    [ObservableProperty] private FilterOption<int?>? _selectedAsset;
    [ObservableProperty] private string _disciplineSummary = "";

    public bool IsEmpty => Entries.Count == 0;

    partial void OnSelectedEmotionChanged(FilterOption<string>? value)
    {
        if (!_loadingFilters) _ = TryRunAsync(ReloadEntriesAsync);
    }

    partial void OnSelectedAssetChanged(FilterOption<int?>? value)
    {
        if (!_loadingFilters) _ = TryRunAsync(ReloadEntriesAsync);
    }

    protected override async Task LoadAsync()
    {
        var assets = await portfolio.GetAssetsAsync();
        var selectedId = SelectedAsset?.Value;

        _loadingFilters = true;
        AssetOptions.Clear();
        AssetOptions.Add(new FilterOption<int?>("Semua Aset", null));
        foreach (var a in assets) AssetOptions.Add(new FilterOption<int?>(a.TickerSymbol, a.AssetId));
        SelectedAsset = AssetOptions.FirstOrDefault(o => o.Value == selectedId) ?? AssetOptions[0];
        SelectedEmotion ??= EmotionOptions[0];
        _loadingFilters = false;

        await ReloadEntriesAsync();
    }

    private async Task ReloadEntriesAsync()
    {
        var list = await journals.GetAllAsync(SelectedEmotion?.Value, SelectedAsset?.Value);
        Entries.Clear();
        foreach (var e in list) Entries.Add(e);
        OnPropertyChanged(nameof(IsEmpty));

        var tagged = list.Where(e => e.EmotionTag is not null).ToList();
        var rational = tagged.Count(e => e.EmotionTag is "Rasional" or "Riset Mendalam");
        DisciplineSummary = tagged.Count == 0
            ? "Belum ada jurnal dengan tag emosi."
            : $"{list.Count} jurnal · {rational} dari {tagged.Count} keputusan berbasis rasional/riset " +
              $"({Fmt.Percent(rational * 100m / tagged.Count)})";
    }
}
