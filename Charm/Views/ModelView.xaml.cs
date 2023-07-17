﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Charm.Objects;
using HelixToolkit.Wpf.SharpDX;

namespace Charm.Views;

public partial class ModelView : UserControl
{
    public ModelView()
    {
        InitializeComponent();

        // todo improve this
        // StaticListItemModel.Viewport = Viewport;
    }

    // public ELOD GetSelectedLod()
    // {
    //     ELOD selected = (ELOD)LodCombobox.SelectedIndex;
    //     return selected;
    // }

    public int GetSelectedGroupIndex()
    {
        if (GroupsCombobox.SelectedItem == null)
            return -1;
        string selected = (GroupsCombobox.SelectedItem as ComboBoxItem).Content as string;
        if (selected == String.Empty)
            return -1;
        string i = selected.Split("Group ")[1].Split("/")[0];
        int index = int.Parse(i);
        return index - 1;
    }

    private Action _loadModelFunc = null;
    private bool _bFromSelectionChange = false;

    public void SetModelFunction(Action action)
    {
        _loadModelFunc = action;
    }

    private void LodCombobox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // We need the LoadEntity function bound with its data
        if (_loadModelFunc != null)
        {
            _loadModelFunc();
        }
    }

    private void GroupsCombobox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _bFromSelectionChange = true;
        if (_loadModelFunc != null)
        {
            _loadModelFunc();
        }

        _bFromSelectionChange = false;
    }

    public void SetGroupIndices(HashSet<int> hashSet)
    {
        if (_bFromSelectionChange || hashSet.Count == 0)
            return;

        GroupsCombobox.Items.Clear();
        var l = hashSet.ToList();
        if (l != null)
        {
            l.Sort();
            int max = l.Last();
            foreach (var i in l)
            {
                GroupsCombobox.Items.Add(new ComboBoxItem
                {
                    Content = $"Group {i + 1}/{max + 1}",
                    IsSelected = i == l.First()
                });
            }
        }
    }
}

