using Jotunn.Managers;
using UnityEngine;

namespace CreatureCodex
{
    internal static class CodexBook
    {
        private static CodexBookView _view;
        private static string _uiLanguage;
        private static bool _blocking;
        private static bool _listening;
        private static bool _typedLastFrame;

        internal static bool IsOpen => _view != null && _view.Root != null && _view.Root.activeSelf;

        /// <summary>True while the search field has the keyboard, or had it last frame.</summary>
        internal static bool IsTyping => IsOpen && (_typedLastFrame || _view.IsTyping);

        internal static void Invalidate()
        {
            SetOpen(false);
            if (_view != null)
            {
                _view.Destroy();
                _view = null;
            }
        }

        internal static void Toggle()
        {
            SetOpen(!IsOpen);
        }

        internal static void Close()
        {
            SetOpen(false);
        }

        internal static void Tick()
        {
            if (!IsOpen)
            {
                _typedLastFrame = false;
                return;
            }

            // Esc and Enter end the edit inside the input field, which may run before or after this
            // Update; checking the previous frame too keeps that key from also closing the book.
            var typing = _view.IsTyping;
            var wasTyping = _typedLastFrame;
            _typedLastFrame = typing;
            if (typing || wasTyping)
            {
                return;
            }

            if (ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB"))
            {
                SetOpen(false);
                return;
            }

            if (ZInput.GetKeyDown(KeyCode.UpArrow) || ZInput.GetKeyDown(KeyCode.LeftArrow))
            {
                _view.Step(-1);
            }
            else if (ZInput.GetKeyDown(KeyCode.DownArrow) || ZInput.GetKeyDown(KeyCode.RightArrow))
            {
                _view.Step(1);
            }
        }

        private static void SetOpen(bool open)
        {
            if (open)
            {
                if (!CreatureCatalog.IsBuilt)
                {
                    Jotunn.Logger.LogWarning("Creature Codex: the creature catalog is not ready yet.");
                    return;
                }
                if (!CreatureDatabase.IsBuilt)
                {
                    Jotunn.Logger.LogWarning("Creature Codex: creature data is still being prepared.");
                    return;
                }

                EnsureUi();
                if (_view == null || _view.Root == null)
                {
                    return;
                }

                _view.Refresh();
                _view.Root.SetActive(true);
                SetBlocking(true);
                Jotunn.Logger.LogInfo("Creature Codex opened.");
                return;
            }

            if (_view != null && _view.Root != null)
            {
                _view.EndTyping();
                _view.SaveLayout();
                _view.Root.SetActive(false);
            }

            _typedLastFrame = false;

            if (_blocking)
            {
                SetBlocking(false);
                Jotunn.Logger.LogInfo("Creature Codex closed.");
            }
        }

        private static void SetBlocking(bool block)
        {
            if (block == _blocking)
            {
                return;
            }

            GUIManager.BlockInput(block);
            _blocking = block;
        }

        private static void EnsureUi()
        {
            var language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "English";
            if (_uiLanguage != language && _view != null)
            {
                _view.Destroy();
                _view = null;
            }
            _uiLanguage = language;

            if (!_listening)
            {
                CodexProgress.Changed += OnProgressChanged;
                _listening = true;
            }

            if (_view == null || _view.Root == null)
            {
                _view = CodexBookView.TryBuild();
            }
        }

        private static void OnProgressChanged()
        {
            if (IsOpen)
            {
                _view.Refresh();
            }
        }
    }
}
