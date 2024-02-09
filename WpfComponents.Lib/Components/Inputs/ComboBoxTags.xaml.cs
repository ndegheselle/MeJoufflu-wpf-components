﻿using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace WpfComponents.Lib.Components.Inputs
{
    /// <summary>
    /// Either get the display member path or the ToString() of the object
    /// </summary>
    public class DisplayMemberPathConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == null)
                return string.Empty;

            if (values[1] == DependencyProperty.UnsetValue)
                return values[0].ToString();

            string lDisplayMemberPath = (string)values[1];
            if (!string.IsNullOrEmpty(lDisplayMemberPath))
                return values[0].GetType().GetProperty(lDisplayMemberPath)?.GetValue(values[0]).ToString();
            else
                return values[0].ToString();
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        { throw new NotSupportedException(); }
    }

    public class ComboBoxTags : ComboBoxSearch, INotifyPropertyChanged
    {
        public class RemoveSelectedCommand : ICommand
        {
            public event EventHandler? CanExecuteChanged;

            private readonly ComboBoxTags _comboBoxTags;

            public RemoveSelectedCommand(ComboBoxTags comboBoxTags) { _comboBoxTags = comboBoxTags; }

            public bool CanExecute(object? parameter) { return true; }

            public void Execute(object? parameter)
            {
                if (parameter == null)
                    return;

                _comboBoxTags.InternalSelectedItems.Remove(parameter);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        #region Dependency Properties
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(
            "SelectedItems",
            typeof(IList),
            typeof(ComboBoxTags),
            new FrameworkPropertyMetadata(null, (o, e) => ((ComboBoxTags)o).OnSelectedItemsChanged()));

        public IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        void OnSelectedItemsChanged()
        {
            if (SelectedItems == null || InternalSelectedItems == SelectedItems)
                return;

            InternalSelectedItems = new ObservableCollection<object>();
            foreach (var item in SelectedItems)
            {
                if (Items.Contains(item))
                    InternalSelectedItems.Add(item);
            }

            InternalSelectedItems.CollectionChanged += InternalSelectedItems_CollectionChanged;
        }
        #endregion

        #region Properties
        public ObservableCollection<object> InternalSelectedItems { get; set; } = new ObservableCollection<object>();

        public bool AllowAdd { get; set; } = false;

        public RemoveSelectedCommand RemoveSelectedCmd { get; }

        // Only add to selection then clicking or pressing enter (like combobox with IsEditable = false)
        // Sad that the combobox doesn't allow to set this behavior
        private bool _ignoreNextSelection = false;
        #endregion

        public ComboBoxTags()
        {
            RemoveSelectedCmd = new RemoveSelectedCommand(this);
            InternalSelectedItems.CollectionChanged += InternalSelectedItems_CollectionChanged;
        }

        /// <summary>
        /// Handle update the SelectedItems property when the internal list is modified
        /// </summary>
        private void InternalSelectedItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var bindingExpression = GetBindingExpression(SelectedItemsProperty);
            // TwoWay or OneWayToSource : update the source directly to avoid losing the reference
            if (bindingExpression?.ParentBinding.Mode == BindingMode.TwoWay ||
                bindingExpression?.ParentBinding.Mode == BindingMode.OneWayToSource)
            {
                SelectedItems.Clear();
                foreach (var item in InternalSelectedItems)
                    SelectedItems.Add(item);
                // For non ObservableCollection
                bindingExpression.UpdateSource();
            }
            // Else copy the internal list so that other controls can bind to it
            else
            {
                SelectedItems = InternalSelectedItems;
            }
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            if (_ignoreNextSelection)
            {
                _ignoreNextSelection = false;
                return;
            }

            AddSelectedItem();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
                _ignoreNextSelection = true;

            base.OnPreviewKeyDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Back && string.IsNullOrEmpty(Text) && InternalSelectedItems.Count > 0)
            {
                InternalSelectedItems.RemoveAt(InternalSelectedItems.Count - 1);
            }
            else if (e.Key == Key.Enter && SelectedItem != null)
            {
                AddSelectedItem();
            }

            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); }

        protected override bool DoesItemPassFilter(object value)
        {
            // If the item is already selected, don't show it in the list
            if (InternalSelectedItems.Contains(value) == true)
                return false;

            return base.DoesItemPassFilter(value);
        }

        private void AddSelectedItem()
        {
            if (SelectedItem == null)
                return;
            InternalSelectedItems.Add(SelectedItem);
            Text = string.Empty;
        }
    }
}
