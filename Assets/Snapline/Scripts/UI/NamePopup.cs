using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// YOUR NAME: what the player is called on the world board.
    ///
    /// One field, one SAVE. It says what is wrong with a name before the player taps SAVE, because a
    /// rejection after the fact reads as the game being broken rather than the name being too short.
    ///
    /// Saving can fail for reasons the player did not cause - no network, not signed in yet - so the
    /// card stays open and says so instead of closing on a name that never took.
    /// </summary>
    public sealed class NamePopup : CandyPopup
    {
        public event Action Saved;

        private InputField _field;
        private Text _hint;
        private Button _save;

        public void Init(RectTransform parent)
        {
            // Taller so both buttons clear each other and the card's own border.
            BuildShell(parent, "Name", "YOUR NAME", new Vector2(880f, 1020f), -20f, true, 660f, 78);

            W.Text("Blurb", Card, "This is the name other players see on the world board.", W.Top,
                   new Vector2(0f, -250f), new Vector2(740f, 120f), 36, CandyStyle.Cocoa, Color.white)
             .horizontalOverflow = HorizontalWrapMode.Wrap;

            Image box = W.Rounded("Field", Card, CandyText.Hex(0xFBE5D3), CandyText.Hex(0xE8B98F), W.Top,
                                  new Vector2(0f, -420f), new Vector2(700f, 130f), 36f);

            Text text = W.Text("Text", box.transform, "", W.Centre, Vector2.zero, new Vector2(620f, 100f),
                               64, CandyStyle.Navy, Color.white);
            text.font = Design.Display;
            text.supportRichText = false;

            Text placeholder = W.Text("Placeholder", box.transform, "TAP TO TYPE", W.Centre, Vector2.zero,
                                      new Vector2(620f, 100f), 54, CandyStyle.Cocoa, Color.white);
            placeholder.font = Design.Display;

            _field = box.gameObject.AddComponent<InputField>();
            _field.textComponent = text;
            _field.placeholder = placeholder;
            _field.characterLimit = PlayerName.MaxLength;
            _field.lineType = InputField.LineType.SingleLine;
            _field.onValueChanged.AddListener(_ => Validate());

            _hint = W.Text("Hint", Card, "", W.Top, new Vector2(0f, -540f), new Vector2(720f, 70f),
                           34, CandyStyle.Pink, Color.white);

            _save = W.Pill("Save", Card, "pill_green", "SAVE", W.Bottom, new Vector2(0f, 300f),
                           new Vector2(580f, 156f), 74, CandyStyle.OnGreen, sprinkles: true, shine: true, pulse: 0.025f);
            _save.onClick.AddListener(Save);

            Button cancel = W.Pill("Cancel", Card, "pill_white", "NOT NOW", W.Bottom, new Vector2(0f, 130f),
                                   new Vector2(500f, 112f), 50, CandyStyle.OnBlue, tint: W.CandyBlue);
            cancel.onClick.AddListener(() => Close());
        }

        public void Show()
        {
            _field.text = PlayerName.Current;
            Validate();
            Present();

            // A field nobody can type into is the commonest way this screen fails on a phone.
            _field.Select();
            _field.ActivateInputField();
        }

        /// <summary>Opens the card with a name already typed. The smoke harness uses it.</summary>
        public void ShowForHarness(string typed)
        {
            Show();
            _field.text = typed;
            Validate();
        }

        /// <summary>Taps SAVE. The smoke harness uses it.</summary>
        public void SaveForHarness() => Save();

        private void Validate()
        {
            string problem = PlayerName.Problem(_field.text);
            _hint.text = problem ?? string.Empty;
            _save.interactable = problem == null;
            _save.GetComponent<Image>().color = problem == null ? Color.white : new Color(0.78f, 0.82f, 0.78f, 1f);
        }

        private async void Save()
        {
            string wanted = _field.text;
            if (PlayerName.Problem(wanted) != null) return;

            _save.interactable = false;
            _hint.text = "Saving...";

            bool ok = await PlayerName.SetAsync(wanted);
            if (!ok)
            {
                _hint.text = "Could not save that name. Try again in a moment.";
                _save.interactable = true;
                Sound.Deny();
                return;
            }

            Sound.Prize();
            Fx.Instance?.Sparkles(Card.position, 14, 220f, 70f);
            Saved?.Invoke();
            Close();
        }
    }
}
