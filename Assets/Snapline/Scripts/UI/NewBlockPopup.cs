using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using Snapline.Core;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// NEW BLOCK!: shown the first time a level contains a kind of special block the player has
    /// never met, before they can place a piece. The block itself, what it is called, what it does,
    /// and a little before-and-after strip showing it happen.
    ///
    /// Once per kind, remembered on the device. A player who has met the stone does not want the
    /// stone explained again at level 90.
    /// </summary>
    public sealed class NewBlockPopup : CandyPopup
    {
        private const string SeenPrefix = "snapline.seen.special.";

        public event Action Done;

        private Image _hero;
        private Image _heroIcon;
        private Text _name;
        private Text _description;
        private Image _before;
        private Image _beforeIcon;
        private Image _after;
        private Image _afterIcon;
        private Text _afterText;
        private Text _caption;
        private Button _ok;

        public static bool HasSeen(Special kind) => PlayerPrefs.GetInt(SeenPrefix + (int)kind, 0) == 1;

        public static void MarkSeen(Special kind)
        {
            PlayerPrefs.SetInt(SeenPrefix + (int)kind, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets every introduction. The screenshot harness only.</summary>
        public static void ResetSeen()
        {
            foreach (Special kind in new[] { Special.Stone, Special.Bomb, Special.Gift })
                PlayerPrefs.DeleteKey(SeenPrefix + (int)kind);
            PlayerPrefs.Save();
        }

        public void Init(RectTransform parent)
        {
            BuildShell(parent, "NewBlock", "NEW BLOCK!", new Vector2(880f, 1180f), -20f, true, 700f, 84);

            W.Starburst(Card, W.Top, new Vector2(0f, -310f), 540f, 20f);
            _hero = W.Img("Hero", Card, "tile_navy", W.Top, new Vector2(0f, -310f), new Vector2(230f, 230f));
            _heroIcon = W.Img("Icon", _hero.transform, "coin", W.Centre, Vector2.zero, new Vector2(190f, 190f));
            CandyPress bob = _hero.gameObject.AddComponent<CandyPress>();
            bob.Bob = 10f;
            bob.Click = false;
            _hero.gameObject.AddComponent<Glint>().Every = 0.6f;

            _name = W.Text("Name", Card, "", W.Top, new Vector2(0f, -500f), new Vector2(820f, 100f),
                           74, CandyStyle.Blue, Color.white);
            _name.font = Design.Display;

            _description = W.Text("Description", Card, "", W.Top, new Vector2(0f, -640f), new Vector2(740f, 190f),
                                  40, CandyStyle.Cocoa, Color.white);
            _description.horizontalOverflow = HorizontalWrapMode.Wrap;

            Image strip = W.Rounded("Strip", Card, CandyText.Hex(0xFBE5D3), CandyText.Hex(0xF1CDB5), W.Top,
                                    new Vector2(0f, -838f), new Vector2(700f, 170f), 40f);
            _before = W.Img("Before", strip.transform, "tile_navy", W.Centre, new Vector2(-200f, 8f), new Vector2(120f, 120f));
            _beforeIcon = W.Img("Icon", _before.transform, "coin", W.Centre, Vector2.zero, new Vector2(100f, 100f));
            W.Text("Arrow", strip.transform, "→", W.Centre, new Vector2(-50f, 8f), new Vector2(90f, 90f),
                   70, CandyStyle.Pink, Color.white).font = Design.Display;
            _after = W.Img("After", strip.transform, "tile_navy", W.Centre, new Vector2(110f, 8f), new Vector2(120f, 120f));
            _afterIcon = W.Img("Icon", _after.transform, "coin", W.Centre, Vector2.zero, new Vector2(100f, 100f));
            _afterText = W.Text("AfterText", strip.transform, "", W.Centre, new Vector2(170f, 8f), new Vector2(320f, 90f),
                                52, CandyStyle.Gold, Color.white);
            _afterText.font = Design.Display;
            _caption = W.Text("Caption", strip.transform, "", W.Bottom, new Vector2(0f, 18f), new Vector2(680f, 36f),
                              28, CandyStyle.Cocoa, Color.white);

            _ok = W.Pill("Ok", Card, "pill_green", "GOT IT!", W.Bottom, new Vector2(0f, 110f), new Vector2(540f, 150f),
                         76, CandyStyle.OnGreen, sprinkles: true, shine: true, pulse: 0.025f);
            _ok.onClick.AddListener(Finish);
        }

        public void Show(Special kind)
        {
            MarkSeen(kind);
            Sprite block = ArtKit.Block(kind == Special.Bomb ? 0 : 4);

            switch (kind)
            {
                case Special.Stone:
                    SetTile(_hero, _heroIcon, SpecialArt.Stone(), null);
                    _name.text = "STONE BLOCK";
                    _description.text = "Too tough for one clear. Clear its line once to crack it, then again to smash it.";
                    SetTile(_before, _beforeIcon, SpecialArt.Stone(), null);
                    SetTile(_after, _afterIcon, SpecialArt.CrackedStone(), null);
                    _after.gameObject.SetActive(true);
                    _afterText.text = "";
                    _caption.text = "1st clear cracks it  •  2nd clear smashes it";
                    break;

                case Special.Bomb:
                    SetTile(_hero, _heroIcon, block, SpecialArt.Bomb());
                    _name.text = "BOMB BLOCK";
                    _description.text = "Clear a line through it and BOOM — it blasts every block around it. Bombs in the blast go off too!";
                    SetTile(_before, _beforeIcon, block, SpecialArt.Bomb());
                    _after.gameObject.SetActive(false);
                    _afterText.text = "BOOM!";
                    _caption.text = "blasts the 3 × 3 around it";
                    break;

                default:
                    SetTile(_hero, _heroIcon, block, SpecialArt.Gift());
                    _name.text = "GIFT BLOCK";
                    _description.text = $"Clear a line through it to unwrap +{GameRun.GiftMoves} extra moves for this level.";
                    SetTile(_before, _beforeIcon, block, SpecialArt.Gift());
                    _after.gameObject.SetActive(false);
                    _afterText.text = $"+{GameRun.GiftMoves} MOVES";
                    _caption.text = "more moves, more stars";
                    break;
            }

            Present();
            Reveal(_hero, 0.15f);
            Reveal(_name, 0.25f);
            Reveal(_ok, 0.4f);
            Sound.Unlock();
            Tween.Delay(0.3f, () => Fx.Instance?.Sparkles(_hero.transform.position, 14, 160f, 80f));
        }

        private static void SetTile(Image tile, Image icon, Sprite base_, Sprite overlay)
        {
            tile.sprite = base_;
            icon.gameObject.SetActive(overlay != null);
            if (overlay != null) icon.sprite = overlay;
        }

        private void Finish() => Close(() => Done?.Invoke());

        /// <summary>GOT IT, as if tapped.</summary>
        public void Dismiss() => Finish();

        protected override void OnCloseButton() => Finish();
    }
}
