// The title screen, the pause menu, and the settings panel.
//
// The game had none of these. It booted straight into a running city, and there was no way to
// pause, restart, change the volume or leave — the only exit was the window's close button.
//
// They are built the way everything else here is: from code, over the live 3D view, in the same
// dark glass the HUD uses. The city keeps rendering behind them on purpose. A title screen over
// a still image is a poster; a title screen over the city you are about to govern, with the
// crowd still walking, is an invitation.

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mesruiyet.UI
{
    public static class Menus
    {
        // ---------------------------------------------------------------- title

        /// <summary>
        /// Shown at boot, over the city, before the first turn is playable. The buttons carry
        /// `name`s so the agent can press them: it opens the game the same way a player does
        /// rather than through a private back door.
        /// </summary>
        public static VisualElement Title(Action onStart, Action onSettings, Action onQuit)
        {
            var root = Scrim("panel_title", 0.72f);

            var card = UiKit.Glass().Pad(38, 46);
            card.style.alignItems = Align.Center;
            card.style.minWidth = 430;

            var title = UiKit.Text("MEŞRUİYET", 52, UiKit.Ink, FontStyle.Bold);
            title.style.letterSpacing = 6f;
            card.Add(title);

            var sub = UiKit.Text("Bir Şehir Kurma ve Kaybetme Simülasyonu", 13, UiKit.Muted)
                           .Margin(top: 6, bottom: 4);
            sub.style.letterSpacing = 1.4f;
            card.Add(sub);

            var rule = new VisualElement();
            rule.style.height = 1;
            rule.style.width = 260;
            rule.style.marginTop = 20; rule.style.marginBottom = 22;
            rule.style.backgroundColor = UiKit.Hairline;
            card.Add(rule);

            var blurb = UiKit.Text(
                "Delta'da dört yüz kişi, boş bir ızgara ve bir dönem var.\n" +
                "Altmış tur sonra şehri bir vârise devretmeyeceksiniz.\n" +
                "İçinde duracaksınız.",
                12.5f, UiKit.Dim);
            blurb.style.whiteSpace = WhiteSpace.Normal;
            blurb.style.unityTextAlign = TextAnchor.MiddleCenter;
            blurb.Margin(bottom: 26);
            card.Add(blurb);

            card.Add(Primary("btn_new_term", "YENİ DÖNEM", onStart));
            card.Add(Secondary("btn_title_settings", "AYARLAR", onSettings));
            card.Add(Secondary("btn_title_quit", "ÇIKIŞ", onQuit));

            root.Add(card);
            return root;
        }

        // ---------------------------------------------------------------- pause

        public static VisualElement Pause(Action onResume, Action onSettings,
                                          Action onRestart, Action onTitle, Action onQuit)
        {
            var root = Scrim("panel_pause", 0.62f);

            var card = UiKit.Glass().Pad(30, 40);
            card.style.alignItems = Align.Center;
            card.style.minWidth = 360;

            card.Add(UiKit.Text("DURAKLATILDI", 22, UiKit.Ink, FontStyle.Bold).Margin(bottom: 4));
            card.Add(UiKit.Text("Şehir bekliyor.", 12, UiKit.Muted).Margin(bottom: 24));

            card.Add(Primary("btn_resume", "DEVAM", onResume));
            card.Add(Secondary("btn_pause_settings", "AYARLAR", onSettings));
            card.Add(Secondary("btn_restart", "DÖNEMİ YENİDEN KUR", onRestart));
            card.Add(Secondary("btn_to_title", "ANA MENÜ", onTitle));
            card.Add(Secondary("btn_pause_quit", "ÇIKIŞ", onQuit));

            root.Add(card);
            return root;
        }

        // ---------------------------------------------------------------- settings

        /// <summary>
        /// Deliberately short. Every row here changes something the player can hear or feel
        /// immediately; a settings screen full of options that do nothing is worse than none.
        /// </summary>
        public static VisualElement Settings(Action onClose)
        {
            var root = Scrim("panel_settings", 0.7f);

            var card = UiKit.Glass().Pad(28, 34);
            card.style.minWidth = 420;

            card.Add(UiKit.Text("AYARLAR", 20, UiKit.Ink, FontStyle.Bold).Margin(bottom: 20));

            var audio = Core.AudioBus.Instance;

            // ---- volume
            var volRow = UiKit.Row();
            volRow.style.alignItems = Align.Center;
            volRow.style.marginBottom = 14;
            volRow.Add(Label("SES"));

            var volValue = UiKit.Text("70", 13, UiKit.Ink, FontStyle.Bold);
            volValue.style.width = 38;
            volValue.style.unityTextAlign = TextAnchor.MiddleRight;

            var slider = new Slider(0f, 1f) { name = "sld_volume" };
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 10; slider.style.marginRight = 10;
            slider.value = audio != null ? audio.Master : 0.7f;
            slider.RegisterValueChangedCallback(e =>
            {
                if (Core.AudioBus.Instance != null) Core.AudioBus.Instance.Master = e.newValue;
                volValue.text = Mathf.RoundToInt(e.newValue * 100f).ToString();
            });
            volValue.text = Mathf.RoundToInt(slider.value * 100f).ToString();

            volRow.Add(slider);
            volRow.Add(volValue);
            card.Add(volRow);

            // ---- mute
            var muteRow = UiKit.Row();
            muteRow.style.alignItems = Align.Center;
            muteRow.style.marginBottom = 14;
            muteRow.Add(Label("SUSTUR"));

            var mute = new Button { name = "btn_mute", text = audio != null && audio.Muted ? "AÇIK" : "KAPALI" };
            StyleToggle(mute, audio != null && audio.Muted);
            mute.clicked += () =>
            {
                var bus = Core.AudioBus.Instance;
                if (bus == null) return;
                bus.ToggleMute();
                mute.text = bus.Muted ? "AÇIK" : "KAPALI";
                StyleToggle(mute, bus.Muted);
            };
            muteRow.Add(mute);
            card.Add(muteRow);

            var note = UiKit.Text("Susturmayı oyun içinde M tuşu da açıp kapatır.", 11.5f, UiKit.Dim);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.Margin(top: 6, bottom: 22);
            card.Add(note);

            var close = Primary("btn_settings_close", "KAPAT", onClose);
            close.style.alignSelf = Align.Center;
            card.Add(close);

            root.Add(card);
            return root;
        }

        // ---------------------------------------------------------------- parts

        static VisualElement Scrim(string name, float darkness)
        {
            var root = new VisualElement { name = name };
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
            root.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, darkness);
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            return root;
        }

        static Label Label(string text)
        {
            var l = UiKit.Text(text, 11.5f, UiKit.Muted, FontStyle.Bold);
            l.style.letterSpacing = 1.2f;
            l.style.width = 96;
            return l;
        }

        static Button Primary(string name, string text, Action onClick)
        {
            var b = new Button { name = name, text = text };
            b.style.width = 300;
            b.style.height = 44;
            b.style.marginBottom = 10;
            b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0;
            b.style.backgroundColor = UiKit.Amber;
            b.style.color = UiKit.Hex("#1A1206");
            b.style.fontSize = 14;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.letterSpacing = 1.6f;
            b.Radius(11).Border(0, Color.clear);
            if (onClick != null) b.clicked += onClick;
            return b;
        }

        static Button Secondary(string name, string text, Action onClick)
        {
            var b = new Button { name = name, text = text };
            b.style.width = 300;
            b.style.height = 40;
            b.style.marginBottom = 8;
            b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0;
            b.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
            b.style.color = UiKit.Ink;
            b.style.fontSize = 12.5f;
            b.style.letterSpacing = 1.4f;
            b.Radius(10).Border(1, UiKit.Hairline);
            if (onClick != null) b.clicked += onClick;
            return b;
        }

        static void StyleToggle(Button b, bool on)
        {
            b.style.width = 96;
            b.style.height = 30;
            b.style.marginLeft = 0; b.style.marginRight = 0;
            b.style.marginTop = 0; b.style.marginBottom = 0;
            b.style.fontSize = 11.5f;
            b.style.letterSpacing = 1.2f;
            b.style.color = on ? UiKit.Hex("#1A1206") : UiKit.Ink;
            b.style.backgroundColor = on ? UiKit.Amber : new Color(1f, 1f, 1f, 0.05f);
            b.Radius(8).Border(1, on ? Color.clear : UiKit.Hairline);
        }
    }
}
