using System;
using System.Collections.Generic;
using System.Text;

namespace DomainLayer
{
    public sealed class ComboBoxItem
    {
        public ComboBoxItem(string text, string value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }
        public string Value { get; }

        public override string ToString() => Text;
    }
}
