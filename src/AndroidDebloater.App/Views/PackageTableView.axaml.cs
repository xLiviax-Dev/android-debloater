using System;
using System.Collections.Generic;
using System.Linq;
using AndroidDebloater.App.ViewModels;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AndroidDebloater.App.Views;

/// <summary>
/// Wiederverwendbare Paket-Tabelle (Spalten, Icon-Zelle, Zeilen-Aktionsmenü und
/// Rechtsklick-Kontextmenü). Wird je Kategorie-Tab einmal eingebunden; die
/// Daten kommen aus <see cref="MainViewModel.FilteredPackages"/> des
/// umgebenden Fensters.
/// </summary>
public partial class PackageTableView : UserControl
{
    public PackageTableView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Lädt Icon und Anzeigename des angeklickten Pakets nach. Der Klick wird hier
    /// entgegengenommen, weil die Zelle selbst nur das Paket als Datenkontext hat;
    /// der Befehl liegt aber am Fenster-ViewModel.
    /// </summary>
    private void OnLoadIconClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Button { DataContext: AndroidPackage package })
        {
            viewModel.LoadIconCommand.Execute(package);
        }
    }

    /// <summary>
    /// Zeile: öffnet das Aktionsmenü an der Schaltfläche. Die Einträge sind
    /// genau die Aktionen, die für dieses Paket gerade erlaubt sind (M12).
    /// </summary>
    private void OnActionMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Button { DataContext: AndroidPackage package } button)
        {
            OpenActionMenu(viewModel, package, button);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Rechtsklick auf eine Zeile öffnet dasselbe Aktionsmenü wie die Spalte.
    /// Die Tabelle selbst kennt keine Zeilenselektion, daher wird die Zeile
    /// über den Treffer (Source) ermittelt.
    /// </summary>
    private void OnRowContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel
            || e.Source is not Visual source)
        {
            return;
        }

        var row = source.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault();
        if (row?.DataContext is not AndroidPackage package)
        {
            return;
        }

        if (OpenActionMenu(viewModel, package, row))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Baut das Menü aus den möglichen Aktionen des Pakets und öffnet es am
    /// Ziel. Liefert false, wenn es nichts anzubieten gibt (gesperrtes Paket).
    /// </summary>
    private static bool OpenActionMenu(MainViewModel viewModel, AndroidPackage package, Control target)
    {
        var actions = ActionPlanner.AvailableActions(package);
        if (actions.Count == 0)
        {
            return false;
        }

        var items = new List<MenuItem>(actions.Count);
        foreach (var plan in actions)
        {
            var item = new MenuItem { Header = plan.Label };
            item.Click += (_, _) => viewModel.ActCommand.Execute(plan);
            items.Add(item);
        }

        var menu = new ContextMenu { ItemsSource = items, PlacementTarget = target };
        menu.Open(target);
        return true;
    }
}
