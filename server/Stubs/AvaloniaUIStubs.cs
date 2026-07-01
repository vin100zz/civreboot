// Stubs for Avalonia UI dialogs used in TextBoxDialogs.cs, GameTools.cs, LanguageTools.cs.
// On the server, text input comes from the WebSocket; these return sensible defaults.
using OpenCivOne.UI;

namespace OpenCivOne
{
    // Stub for EditBox.axaml — ShowEditBoxDialog is called by TextBoxDialogs
    internal static class EditBox
    {
        public static string? ShowEditBoxDialog(object? parent, string title, string prompt,
            string? defaultText, int maxLength, bool readOnly)
        {
            // Return the default/pre-assigned name so city founding succeeds.
            // Returning null causes CityNameDialog to return 0 → Overlay_20 returns -1 → city not founded.
            return string.IsNullOrEmpty(defaultText) ? "City" : defaultText;
        }
    }

    // Stub for the Avalonia MessageBox dialog — matches all call signatures in OpenCivOne
    internal static class MessageBox
    {
        // TextBoxDialogs: MessageBox.Show(window, text, caption, icon)
        public static MessageBoxResult Show(object? parent, string text, string caption,
            MessageBoxIcon icon)
        {
            return MessageBoxResult.OK;
        }

        // GameTools: MessageBox.Show(text, caption, icon, buttons)
        public static MessageBoxResult Show(string text, string caption,
            MessageBoxIcon icon, MessageBoxButtons buttons)
        {
            return MessageBoxResult.OK;
        }

        // LanguageTools: MessageBox.Show(text, caption, icon)
        public static MessageBoxResult Show(string text, string caption,
            MessageBoxIcon icon)
        {
            return MessageBoxResult.OK;
        }
    }
}
