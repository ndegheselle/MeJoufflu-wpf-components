﻿using Joufflu.Inputs.Format;
using PropertyChanged;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Joufflu.Inputs.Format
{
    [TemplatePart(Name = "PART_ClearButton", Type = typeof(Button))]
    [TemplatePart(Name = "PART_UpButton", Type = typeof(Button))]
    [TemplatePart(Name = "PART_DownButton", Type = typeof(Button))]
    public class FormatTextBox : TextBox, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName]string? name = null)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }

        public event EventHandler<List<object?>>? ValuesChanged;
        #region Dependency Properties
        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(
            "Values",
            typeof(List<object?>),
            typeof(FormatTextBox),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (o, e) => ((FormatTextBox)o).OnValuesChanged()));

        public List<object?> Values
        {
            get { return (List<object?>)GetValue(ValuesProperty); }
            set { SetValue(ValuesProperty, value); }
        }

        protected virtual void OnValuesChanged()
        {
            if (Groups.Count == 0)
                ParseGroups(Format, GlobalFormat);
            ValuesChanged?.Invoke(this, Values);
            FormatText(Groups);
        }

        public static readonly DependencyProperty GlobalFormatProperty = DependencyProperty.Register(
            "GlobalFormat",
            typeof(string),
            typeof(FormatTextBox),
            new FrameworkPropertyMetadata(
                "",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (o, e) => ((FormatTextBox)o).OnGlobalFormatChanged()));

        public string GlobalFormat
        {
            get { return (string)GetValue(GlobalFormatProperty); }
            set { SetValue(GlobalFormatProperty, value); }
        }

        protected void OnGlobalFormatChanged()
        {
            ParseGroups(Format, GlobalFormat);
            FormatText(Groups);
        }

        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register(
            "Format",
            typeof(string),
            typeof(FormatTextBox),
            new FrameworkPropertyMetadata(
                "",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (o, e) => ((FormatTextBox)o).OnFormatChanged()));

        public string Format
        {
            get { return (string)GetValue(FormatProperty); }
            set { SetValue(FormatProperty, value); }
        }

        protected void OnFormatChanged()
        {
            ParseGroups(Format, GlobalFormat);
            FormatText(Groups);
        }
        #endregion

        #region Properties

        #region Options
        public bool AllowSelectionOutsideGroups { get; set; } = false;

        public bool ShowDeleteButton { get; set; } = true;

        public bool ShowIncrementsButtons { get; set; } = true;

        public int IncrementValue { get; set; } = 1;
        #endregion
        private int _selectedGroupIndex = -1;

        public int SelectedGroupIndex
        {
            get { return _selectedGroupIndex; }
            set
            {
                _selectedGroupIndex = value;
                if (_selectedGroupIndex < 0)
                {
                    SelectedGroup = null;
                }
                else
                {
                    SelectedGroup = Groups[value];
                }
            }
        }

        public BaseGroup? SelectedGroup { get; set; }

        public List<BaseGroup> Groups = new List<BaseGroup>();

        private string _outputFormat = "";
        private bool _isSelectionChanging = false;

        // UI Parts
        private Button? _clearButton;
        private Button? ClearButton
        {
            get { return _clearButton; }
            set
            {
                _clearButton = value;

                if (_clearButton != null)
                    _clearButton.Click += ClearButton_Click;
            }
        }

        private Button? _upButton;
        private Button? UpButton
        {
            get { return _upButton; }
            set
            {
                _upButton = value;
                if (_upButton != null)
                    _upButton.Click += UpButton_Click;
            }
        }

        private Button? _downButton;
        private Button? DownButton
        {
            get { return _downButton; }
            set
            {
                _downButton = value;

                if (_downButton != null)
                    _downButton.Click += DownButton_Click;
            }
        }
        #endregion

        public override void OnApplyTemplate()
        {
            ClearButton = (Button)GetTemplateChild("PART_ClearButton");
            UpButton = (Button)GetTemplateChild("PART_UpButton");
            DownButton = (Button)GetTemplateChild("PART_DownButton");

            base.OnApplyTemplate();
        }

        #region UI Events
        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            base.OnPreviewTextInput(e);

            // Get the new text based on the input and the current selection

            bool validInput = SelectedGroup?.OnInput(e.Text) ?? false;
            if (validInput)
            {
                UpdateCurrentValue();
                SelectedGroup?.OnAfterInput();
            }

            e.Handled = true;
        }

        [SuppressPropertyChangedWarnings]
        protected override void OnSelectionChanged(RoutedEventArgs e)
        {
            if (_isSelectionChanging)
                return;

            base.OnSelectionChanged(e);

            int currentRegexGroupIndex = -1;
            for (int i = 0; i < Groups.Count; i++)
            {
                if (SelectionStart >= Groups[i].Index && SelectionStart <= Groups[i].Index + Groups[i].Length)
                {
                    currentRegexGroupIndex = i;
                    break;
                }
            }
            // Index of the group minus the first group (the global match)
            SelectedGroupIndex = currentRegexGroupIndex;

            if (SelectedGroupIndex < 0 && AllowSelectionOutsideGroups == false)
            {
                Keyboard.ClearFocus();
                e.Handled = true;
            }

            _isSelectionChanging = true;
            SelectedGroup?.OnSelection();
            _isSelectionChanging = false;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // If escape unfocus the textbox
            if (e.Key == Key.Escape)
            {
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            // If tab select next group
            else if (e.Key == Key.Tab)
            {
                ChangeSelectedGroup(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
                e.Handled = true;
            }
            // If arrow keys change group
            else if (e.Key == Key.Left)
            {
                // Numeric group with global selection allow to go to the previous group
                IBaseNumericGroup? numericGroup = SelectedGroup as IBaseNumericGroup;
                if (numericGroup?.NoGlobalSelection == true)
                {
                    ChangeSelectedGroup(-1);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Right)
            {
                // Numeric group with global selection allow to go to the next group
                IBaseNumericGroup? numericGroup = SelectedGroup as IBaseNumericGroup;
                if (numericGroup?.NoGlobalSelection == true)
                {
                    ChangeSelectedGroup(+1);
                    e.Handled = true;
                }
            }
            // If suppr
            else if (e.Key == Key.Delete)
            {
                if (SelectedGroup != null)
                {
                    SelectedGroup.OnDelete();
                    UpdateCurrentValue();
                }
                e.Handled = true;
            }
            // Backspace
            else if (e.Key == Key.Back)
            {
                if (SelectedGroup != null)
                {
                    SelectedGroup.OnDelete();
                    UpdateCurrentValue();
                }
                e.Handled = true;
            }
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGroup == null)
                ChangeSelectedGroup(1);

            if (SelectedGroup is IBaseNumericGroup numericGroup)
            {
                numericGroup.Increment();

                UpdateCurrentValue();
                SelectedGroup?.OnAfterInput();
            }
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGroup == null)
                ChangeSelectedGroup(1);

            if (SelectedGroup is IBaseNumericGroup numericGroup)
            {
                numericGroup.Decrement();
                UpdateCurrentValue();
                SelectedGroup?.OnAfterInput();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var group in Groups)
                group.OnDelete();
            UpdateValues();
        }
        #endregion

        #region Methods
        public void ChangeSelectedGroup(int delta)
        {
            int newindex = SelectedGroupIndex + delta;
            if (newindex < 0 || newindex >= Groups.Count)
                return;

            if (IsFocused == false)
                Focus();

            Select(Groups[newindex].Index, 0);
        }

        private void FormatText(IEnumerable<BaseGroup> groups)
        {
            // Change text and prevent selection from changing
            _isSelectionChanging = true;
            int selectionStart = SelectionStart;
            int selectionLength = SelectionLength;
            this.Text = string.Format(_outputFormat, groups.ToArray());
            Select(selectionStart, selectionLength);
            _isSelectionChanging = false;
        }

        private void UpdateCurrentValue()
        {
            if (SelectedGroup == null)
                return;

            if (Values == null)
            {
                UpdateValues();
            }
            else
            {
                object? oldValue = Values[SelectedGroupIndex];
                if (oldValue != SelectedGroup.Value)
                    UpdateValues();
            }
        }

        private void UpdateValues()
        {
            // Trigger DP change
            Values = Groups.Select(x => x.Value).ToList();
        }
        #endregion

        #region Parsing
        public void ParseGroups(string format, string globalFormat)
        {
            Groups.Clear();
            List<object> groups = ParseFormatString(format, globalFormat);

            // Create format for output
            StringBuilder outputFormatBuilder = new StringBuilder();
            int paramIndex = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] is BaseGroup groupParam)
                {
                    Groups.Add(groupParam);
                    outputFormatBuilder.Append("{" + paramIndex + "}");
                    paramIndex++;
                }
                else if (groups[i] is string outputPart)
                {
                    outputFormatBuilder.Append(outputPart);
                }
            }

            _outputFormat = outputFormatBuilder.ToString();

            if (Values == null || Values.Count != Groups.Count)
                return;

            // Update group values from parts
            for (int i = 0; i < Groups.Count; i++)
            {
                Groups[i].Value = Values[i];
            }
        }

        private List<object> ParseFormatString(string format, string globalFormat)
        {
            GroupsFactory groupsFactory = new GroupsFactory();
            StringBuilder outputFormatBuilder = new StringBuilder();
            List<object> groups = new List<object>();

            StringBuilder groupBuilder = new StringBuilder();
            int depth = 0;
            int index = 0;
            char previousChar = '\0';
            foreach (char c in format)
            {
                if (c == '}' && previousChar != '\\')
                    depth -= 1;

                if (depth > 0)
                    groupBuilder.Append(c);
                else if (c != '{' && c != '}')
                    outputFormatBuilder.Append(c);

                if (c == '{' && previousChar != '\\')
                    depth += 1;

                // Group params & mask
                if (depth == 0 && c == '}')
                {
                    BaseGroup group = groupsFactory.CreateParams(this, groupBuilder.ToString(), globalFormat);
                    group.Index = index;
                    groups.Add(group);
                    groupBuilder.Clear();

                    index += group.Length;
                }
                else if (depth > 0 && outputFormatBuilder.Length > 0)
                {
                    groups.Add(outputFormatBuilder.ToString());
                    index += outputFormatBuilder.Length;
                    outputFormatBuilder.Clear();
                }

                previousChar = c;
            }

            if (depth > 0)
                throw new Exception("Invalid format, was expecting '}'");

            if (outputFormatBuilder.Length > 0)
                groups.Add(outputFormatBuilder.ToString());

            return groups;
        }
        #endregion
    }

    public abstract class SingleValueFormatTextBox<T> : FormatTextBox
    {
        public event EventHandler<T?>? ValueChanged;

        private T? _previousValue = default;
        public virtual T? Value { get; set; } = default;

        public virtual List<object?> ConvertTo() { return new List<object?>() { Value }; }

        public virtual T? ConvertFrom()
        {
            if (Values.Any(x => x == null))
                return default;
            return (T?)Values.FirstOrDefault();
        }

        [SuppressPropertyChangedWarnings]
        protected virtual void OnValueChanged(DependencyPropertyChangedEventArgs e)
        {
            if (EqualityComparer<T>.Default.Equals(Value, _previousValue))
                return;

            Values = ConvertTo();
            _previousValue = Value;
            ValueChanged?.Invoke(this, Value);
        }

        protected override void OnValuesChanged()
        {
            base.OnValuesChanged();

            var newValue = ConvertFrom();

            if (EqualityComparer<T>.Default.Equals(Value, newValue))
                return;
            Value = newValue;
        }
    }
}
