using Modding;
using System;
using JetBrains.Annotations;
using UnityEngine;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Globalization;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections;

namespace SpeedChanger
{
    public class GlobalSettings
    {
        public bool globalSwitch = true;

        public bool lockSwitch = false;

        public int displayStyle = 0;

        public string tip = "A list of all KeyCodes can be found here: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/KeyCode.html";
        public string speedUpKeybind = "Alpha9";
        public string slowDownKeybind = "Alpha8";
        public string toggleKeybind = "F10";
        public bool restrictToggleToRooms = false;
        public string inputSpeedKeybind = "F11";
        public bool unlimitedSpeed = false;
        
        public float step = 0.05f;
        private float _speed = 1.00f;
        public float speed
        {
            get
            {
                return _speed;
            }
            set
            {
                _speed = (float)Math.Round(value, 2);
            }
        }

    }

    [UsedImplicitly]
    public class SpeedChanger : Mod, IMenuMod, IGlobalSettings<GlobalSettings>
    {
        public override string GetVersion() => "1.1.3.2";

        /* Global settings */
        public static GlobalSettings GS { get; set; } = new GlobalSettings();

        public void OnLoadGlobal(GlobalSettings s)
        {
            GS = s;
            RefreshKeybinds();
        }

        public GlobalSettings OnSaveGlobal()
        {
            return GS;
        }
        private KeyCode _speedUpKeybind;
        private KeyCode _slowDownKeybind;
        private KeyCode _toggleKeybind;
        private KeyCode _inputSpeedKeybind;

        private bool _isLoaded;
        private bool _waitingForToggleRebind;
        private KeyCode _currentToggleKeyBeforeRebind;
        private RebindListener _rebindListener;
        private readonly string[] _toggleMenuValues = new string[1];
        private bool _waitingForInputRebind;
        private KeyCode _currentInputKeyBeforeRebind;
        private readonly string[] _inputMenuValues = new string[1];
        private bool _waitingForSpeedUpRebind;
        private KeyCode _currentSpeedUpKeyBeforeRebind;
        private readonly string[] _speedUpMenuValues = new string[1];
        private bool _waitingForSpeedDownRebind;
        private KeyCode _currentSpeedDownKeyBeforeRebind;
        private readonly string[] _speedDownMenuValues = new string[1];

        private bool _waitingForSpeedInput;
        private string _speedInputBuffer = string.Empty;

        private static readonly string[] ToggleKeyOptions = new[]
        {
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "BackQuote", "Tab", "CapsLock", "LeftShift", "RightShift", "LeftControl", "RightControl", "LeftAlt", "RightAlt", "Space",
            "Alpha1", "Alpha2", "Alpha3", "Alpha4", "Alpha5", "Alpha6", "Alpha7", "Alpha8", "Alpha9", "Alpha0",
            "Minus", "Equals",
            "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P",
            "A", "S", "D", "F", "G", "H", "J", "K", "L",
            "Z", "X", "C", "V", "B", "N", "M",
            "Insert", "Home", "PageUp", "Delete", "End", "PageDown",
            "UpArrow", "DownArrow", "LeftArrow", "RightArrow"
        };

        private static readonly string[] AllowedRoomsForToggle = new[]
        {
            "GG_Workshop",
            "GG_Atrium",
            "GG_Atrium_Roof"
        };

        private static KeyCode ParseKeycode(string value, KeyCode fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("Not Set", StringComparison.OrdinalIgnoreCase))
                return KeyCode.None;
            return Enum.TryParse(value, true, out KeyCode parsed) ? parsed : fallback;
        }

        private void RefreshKeybinds()
        {
            _speedUpKeybind = ParseKeycode(GS.speedUpKeybind, KeyCode.Alpha9);
            _slowDownKeybind = ParseKeycode(GS.slowDownKeybind, KeyCode.Alpha8);
            _toggleKeybind = ParseKeycode(GS.toggleKeybind, KeyCode.F10);
            _inputSpeedKeybind = ParseKeycode(GS.inputSpeedKeybind, KeyCode.F11);
        }

        private int GetToggleKeyIndex()
        {
            int idx = Array.IndexOf(ToggleKeyOptions, GS.toggleKeybind);
            if (idx >= 0) return idx;
            return Array.IndexOf(ToggleKeyOptions, "F10");
        }

        private void ChangeGlobalSwitchState(bool state)
        {
            bool previous = GS.globalSwitch;
            if (GS.globalSwitch == state && _isLoaded == state) return;

            GS.globalSwitch = state;
            UpdateMenuValueDisplay("Global Switch", state ? "On" : "Off", previous ? "On" : "Off");
            SetMenuOptionValue("Global Switch", state ? 1 : 0);

            if (!state)
            {
                if (_isLoaded)
                    Unload();
            }
            else
            {
                if (!_isLoaded)
                    Load();
            }
        }

        /* Handle timescale changes */
        public float SpeedMultiplier
        {
            get
            {
                return GS.globalSwitch ? GS.speed : 1;
            }
            set
            {
                if (value > 0)
                {
                    if (Time.timeScale != 0)
                    {
                        Time.timeScale = value;
                    }
                    GS.speed = value;
                }
            }
        }

        /* Create menu */
        bool IMenuMod.ToggleButtonInsideMenu => true;

        public bool ToggleButtonInsideMenu => true;

        List<IMenuMod.MenuEntry> IMenuMod.GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry)
        {
            return new List<IMenuMod.MenuEntry>
            {
                new IMenuMod.MenuEntry
                {
                    Name = "Global Switch",
                    Description = "Turn mod On/Off",
                    Values = new string[] {
                        "Off",
                        "On",
                    },
                    Saver = opt => ChangeGlobalSwitchState(opt == 1),
                    Loader = () => GS.globalSwitch ? 1 : 0
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Lock Switch",
                    Description = "Lock changing your speed",
                    Values = new string[] {
                        "Off",
                        "On",
                    },
                    Saver = opt => GS.lockSwitch = opt == 1,
                    Loader = () => GS.lockSwitch ? 1 : 0
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Restrict Toggle To Rooms",
                    Description = "Only allow toggle hotkey in GG Workshop/Atrium/Roof",
                    Values = new string[] {
                        "Off",
                        "On",
                    },
                    Saver = opt => GS.restrictToggleToRooms = opt == 1,
                    Loader = () => GS.restrictToggleToRooms ? 1 : 0
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Toggle Mod Hotkey",
                    Description = "Press to choose a new toggle key (same key clears, Esc cancels)",
                    Values = _toggleMenuValues,
                    Saver = opt => StartToggleRebind(),
                    Loader = () =>
                    {
                        _toggleMenuValues[0] = FormatKeyLabel(GS.toggleKeybind);
                        return 0;
                    }
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Unlimited Speed Entry",
                    Description = "Allow entering speed above 10000%",
                    Values = new string[] { "Off", "On" },
                    Saver = opt => GS.unlimitedSpeed = opt == 1,
                    Loader = () => GS.unlimitedSpeed ? 1 : 0
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Input Speed Hotkey",
                    Description = "Press to choose a key for direct speed entry (same key clears, Esc cancels)",
                    Values = _inputMenuValues,
                    Saver = opt => StartInputRebind(),
                    Loader = () =>
                    {
                        _inputMenuValues[0] = FormatKeyLabel(GS.inputSpeedKeybind);
                        return 0;
                    }
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Display Style",
                    Description = "Change how speed is displayed",
                    Values = new string[] {
                        "#.##",
                        "%",
                        "Off"
                    },
                    Saver = opt => {
                        GS.displayStyle = opt;
                        if (opt == 2 && ModDisplay.Instance != null)
                        {
                            ModDisplay.Instance.Destroy();
                            ModDisplay.Instance = null;
                        }
                        else if (opt != 2 && ModDisplay.Instance == null)
                        {
                            ModDisplay.Instance = new ModDisplay();
                        }
                    },
                    Loader = () => GS.displayStyle
                },
                new IMenuMod.MenuEntry
                {
                    Name = $"Slow Game Down (-{GS.step})",
                    Description = "Press to choose a key (same key clears)",
                    Values = _speedDownMenuValues,
                    Saver = opt => StartSpeedDownRebind(),
                    Loader = () =>
                    {
                        _speedDownMenuValues[0] = FormatKeyLabel(GS.slowDownKeybind);
                        return 0;
                    }
                },
                new IMenuMod.MenuEntry
                {
                    Name = $"Speed Game Up (+{GS.step})",
                    Description = "Press to choose a key (same key clears)",
                    Values = _speedUpMenuValues,
                    Saver = opt => StartSpeedUpRebind(),
                    Loader = () =>
                    {
                        _speedUpMenuValues[0] = FormatKeyLabel(GS.speedUpKeybind);
                        return 0;
                    }
                }
            };
        }

        /* Handle freeze frames */
        private ILHook[] _coroutineHooks;

        private static readonly MethodInfo[] FreezeCoroutines = (
            from method in typeof(GameManager).GetMethods()
            where method.Name.StartsWith("FreezeMoment")
            where method.ReturnType == typeof(IEnumerator)
            select method.GetCustomAttribute<IteratorStateMachineAttribute>() into attr
            select attr.StateMachineType into type
            select type.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance)
        ).ToArray();

        private void ScaleFreeze(ILContext il)
        {
            var cursor = new ILCursor(il);

            cursor.GotoNext
            (
                MoveType.After,
                x => x.MatchLdfld(out _),
                x => x.MatchCall<Time>("get_unscaledDeltaTime")
            );

            cursor.EmitDelegate<Func<float>>(() => GS.speed);

            cursor.Emit(OpCodes.Mul);
        }


        /* Set up the hooks */
        public override void Initialize()
        {
            base.Initialize();
            RefreshKeybinds();
            EnsureRebindListener();
            ChangeGlobalSwitchState(GS.globalSwitch);
        }
        public void Load()
        {
            if (_isLoaded) return;
            _isLoaded = true;
            RefreshKeybinds();
            SpeedMultiplier = GS.speed;

            ModHooks.HeroUpdateHook += Update;

            _coroutineHooks = new ILHook[FreezeCoroutines.Length];
            foreach ((MethodInfo coro, int idx) in FreezeCoroutines.Select((mi, idx) => (mi, idx)))
            {
                _coroutineHooks[idx] = new ILHook(coro, ScaleFreeze);
            }

            if (ModDisplay.Instance != null)
            {
                ModDisplay.Instance.Destroy();
                ModDisplay.Instance = null;
            }
            ModDisplay.Instance = new ModDisplay();
        }

        private void Update()
        {
            UpdateSpeedDisplay();
            SpeedMultiplier = SpeedMultiplier;

            if (GS.lockSwitch) return;

            if (_speedUpKeybind != KeyCode.None && Input.GetKeyDown(_speedUpKeybind))
                SpeedMultiplier += GS.step;

            else if (_slowDownKeybind != KeyCode.None && Input.GetKeyDown(_slowDownKeybind))
                SpeedMultiplier -= GS.step;
        }

        private void ListenForToggle()
        {
            if (HandleToggleRebind()) return;
            if (HandleInputRebind()) return;

            if (Input.GetKeyDown(_toggleKeybind))
            {
                bool inMainMenu = IsInMainMenu();

                if (!inMainMenu && GS.restrictToggleToRooms && !IsInAllowedRoom())
                {
                    return;
                }

                if (!inMainMenu && GS.lockSwitch)
                {
                    return;
                }

                bool turningOn = !GS.globalSwitch;

                if (!turningOn && GS.displayStyle != 2 && ModDisplay.Instance != null)
                {
                    ModDisplay.Instance.Display("SpeedChanger: Off");
                }

                ChangeGlobalSwitchState(turningOn);

                if (turningOn && GS.displayStyle != 2 && ModDisplay.Instance != null)
                {
                    ModDisplay.Instance.Display("SpeedChanger: On");
                }
            }

            if (_inputSpeedKeybind != KeyCode.None && Input.GetKeyDown(_inputSpeedKeybind))
            {
                if (_waitingForSpeedInput)
                {
                    _waitingForSpeedInput = false;
                    _speedInputBuffer = string.Empty;
                    ModDisplay.Instance?.HideInput();
                    return;
                }

                if (!GS.globalSwitch) return;

                bool inMainMenu = IsInMainMenu();

                if (!inMainMenu && GS.restrictToggleToRooms && !IsInAllowedRoom())
                {
                    return;
                }
                StartSpeedEntry();
            }
        }

        private void StartToggleRebind()
        {
            _toggleMenuValues[0] = "Set Key...";
            UpdateMenuValueDisplay("Toggle Mod Hotkey", _toggleMenuValues[0]);
            _waitingForToggleRebind = true;
            _currentToggleKeyBeforeRebind = _toggleKeybind;
            if (ModDisplay.Instance != null && GS.displayStyle != 2)
            {
                ModDisplay.Instance.Display("Press a key for toggle (Esc to cancel)");
            }
        }

        private bool HandleToggleRebind()
        {
            if (!_waitingForToggleRebind)
            {
                return false;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(key)) continue;

                if (key == KeyCode.Escape)
                {
                    _waitingForToggleRebind = false;
                    return true;
                }

                if (key == _currentToggleKeyBeforeRebind)
                {
                    GS.toggleKeybind = string.Empty;
                }
                else
                {
                    // avoid conflicts with other hotkeys
                    string keyName = key.ToString();
                    if (IsKeyInUse(keyName, except: "toggle"))
                        GS.toggleKeybind = string.Empty;
                    else
                        GS.toggleKeybind = keyName;
                }

                RefreshKeybinds();
                _waitingForToggleRebind = false;

                string prev = _toggleMenuValues[0];
                _toggleMenuValues[0] = FormatKeyLabel(GS.toggleKeybind);
                UpdateMenuValueDisplay("Toggle Mod Hotkey", _toggleMenuValues[0], prev);

                if (ModDisplay.Instance != null && GS.displayStyle != 2)
                {
                    ModDisplay.Instance.Display(string.IsNullOrEmpty(GS.toggleKeybind)
                        ? "Toggle key cleared"
                        : $"Toggle key set to {GS.toggleKeybind}");
                }

                return true;
            }

            return true;
        }

        private void StartInputRebind()
        {
            _inputMenuValues[0] = "Set Key...";
            UpdateMenuValueDisplay("Input Speed Hotkey", _inputMenuValues[0]);
            _waitingForInputRebind = true;
            _currentInputKeyBeforeRebind = _inputSpeedKeybind;
            if (ModDisplay.Instance != null && GS.displayStyle != 2)
            {
                ModDisplay.Instance.Display("Press a key for input (Esc to cancel)");
            }
        }

        private bool HandleInputRebind()
        {
            if (!_waitingForInputRebind)
            {
                return false;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(key)) continue;

                if (key == KeyCode.Escape)
                {
                    _waitingForInputRebind = false;
                    return true;
                }

                if (key == _currentInputKeyBeforeRebind)
                {
                    GS.inputSpeedKeybind = string.Empty;
                }
                else
                {
                    string keyName = key.ToString();
                    if (IsKeyInUse(keyName, except: "input"))
                        GS.inputSpeedKeybind = string.Empty;
                    else
                        GS.inputSpeedKeybind = keyName;
                }

                RefreshKeybinds();
                _waitingForInputRebind = false;

                string prev = _inputMenuValues[0];
                _inputMenuValues[0] = FormatKeyLabel(GS.inputSpeedKeybind);
                UpdateMenuValueDisplay("Input Speed Hotkey", _inputMenuValues[0], prev);

                if (ModDisplay.Instance != null && GS.displayStyle != 2)
                {
                    ModDisplay.Instance.Display(string.IsNullOrEmpty(GS.inputSpeedKeybind)
                        ? "Input key cleared"
                        : $"Input key set to {GS.inputSpeedKeybind}");
                }

                return true;
            }

            return true;
        }

        private void EnsureRebindListener()
        {
            if (_rebindListener != null) return;

            GameObject go = new GameObject("SpeedChanger_RebindListener");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _rebindListener = go.AddComponent<RebindListener>();
            _rebindListener.Tick = () =>
            {
                HandleToggleRebind();
                HandleInputRebind();
                HandleSpeedUpRebind();
                HandleSpeedDownRebind();
                HandleSpeedEntry();
                ListenForToggle();
                UpdateSpeedDisplay();
            };
        }

        private void UpdateMenuValueDisplay(string entryName, string value, string previousValue = null)
        {
            string Normalize(string s) => string.IsNullOrWhiteSpace(s)
                ? string.Empty
                : new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

            string targetName = Normalize(entryName);
            string targetPrev = Normalize(previousValue ?? string.Empty);
            const string setKeyLiteral = "SETKEY...";
            const string notSetLiteral = "NOTSET";

            foreach (var text in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>(true))
            {
                if (text == null || text.text == null) continue;

                string normalized = Normalize(text.text);
                bool isName = normalized == targetName;
                if (!isName) continue;

                Transform parent = text.transform.parent;
                if (parent == null) continue;

                bool updated = false;
                foreach (var sibling in parent.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    if (sibling == null || sibling.text == null) continue;
                    if (sibling == text) continue;

                    string siblingNorm = Normalize(sibling.text);

                    bool matchesPrev = !string.IsNullOrEmpty(targetPrev) && siblingNorm == targetPrev;
                    bool matchesSetKey = siblingNorm == setKeyLiteral;
                    bool matchesNotSet = siblingNorm == notSetLiteral && Normalize(value) == notSetLiteral;

                    if (matchesPrev || matchesSetKey || matchesNotSet)
                    {
                        sibling.text = value;
                        updated = true;
                    }
                }

                if (!updated)
                {
                    foreach (var sibling in parent.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    {
                        if (sibling == null || sibling == text) continue;
                        sibling.text = value;
                        break;
                    }
                }
            }
        }

        private string FormatKeyLabel(string storedKey)
        {
            if (string.IsNullOrWhiteSpace(storedKey) || storedKey.Equals("Not Set", StringComparison.OrdinalIgnoreCase))
            {
                return "Not Set";
            }
            return storedKey;
        }

        // Try to sync menu option values (toggle indices) when updated programmatically
        private void SetMenuOptionValue(string entryName, int valueIndex)
        {
            string Normalize(string s) => string.IsNullOrWhiteSpace(s)
                ? string.Empty
                : new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

            string targetName = Normalize(entryName);

            foreach (var text in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>(true))
            {
                if (text == null || text.text == null) continue;
                if (Normalize(text.text) != targetName) continue;

                Transform t = text.transform;
                for (int depth = 0; depth < 4 && t != null; depth++, t = t.parent)
                {
                    foreach (var mb in t.GetComponents<MonoBehaviour>())
                    {
                        if (mb == null) continue;
                        var mbType = mb.GetType();
                        var setValue = mbType.GetMethod("SetValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                        if (setValue == null) continue;

                        int maxIndex = valueIndex;
                        var optionTextsField = mbType.GetField("optionTexts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (optionTextsField?.GetValue(mb) is Array arr)
                        {
                            maxIndex = Mathf.Clamp(valueIndex, 0, arr.Length - 1);
                        }

                        setValue.Invoke(mb, new object[] { maxIndex });
                        return;
                    }
                }
            }
        }

        private bool IsKeyInUse(string keyName, string except = "")
        {
            if (string.IsNullOrWhiteSpace(keyName)) return false;
            string key = keyName.Trim();

            bool inToggle = !except.Equals("toggle", StringComparison.OrdinalIgnoreCase) && string.Equals(GS.toggleKeybind, key, StringComparison.OrdinalIgnoreCase);
            bool inInput = !except.Equals("input", StringComparison.OrdinalIgnoreCase) && string.Equals(GS.inputSpeedKeybind, key, StringComparison.OrdinalIgnoreCase);
            bool inUp = !except.Equals("up", StringComparison.OrdinalIgnoreCase) && string.Equals(GS.speedUpKeybind, key, StringComparison.OrdinalIgnoreCase);
            bool inDown = !except.Equals("down", StringComparison.OrdinalIgnoreCase) && string.Equals(GS.slowDownKeybind, key, StringComparison.OrdinalIgnoreCase);

            return inToggle || inInput || inUp || inDown;
        }

        private bool IsInAllowedRoom()
        {
            string sceneName = GameManager.instance?.GetSceneNameString();
            if (string.IsNullOrEmpty(sceneName)) return false;
            return AllowedRoomsForToggle.Contains(sceneName);
        }

        private bool IsInMainMenu()
        {
            string sceneName = GameManager.instance?.GetSceneNameString();
            return sceneName == "Menu_Title";
        }

        private void StartSpeedEntry()
        {
            if (GS.lockSwitch && !IsInMainMenu())
            {
                if (ModDisplay.Instance != null && GS.displayStyle != 2)
                {
                    ModDisplay.Instance.Display("Speed change locked");
                }
                return;
            }

            _waitingForSpeedInput = true;
            _speedInputBuffer = string.Empty;

            if (ModDisplay.Instance != null && GS.displayStyle != 2)
            {
                ModDisplay.Instance.Display("Enter speed % (digits, Enter=apply, Esc=cancel)");
                ModDisplay.Instance.DisplayInput("SetGameSpeed: ");
            }
        }

        private bool HandleSpeedEntry()
        {
            if (!_waitingForSpeedInput)
            {
                return false;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _waitingForSpeedInput = false;
                ModDisplay.Instance?.HideInput();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (_speedInputBuffer.Length > 0)
                {
                    _speedInputBuffer = _speedInputBuffer.Substring(0, _speedInputBuffer.Length - 1);
                    ModDisplay.Instance?.DisplayInput("SetGameSpeed: " + _speedInputBuffer);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_speedInputBuffer.Length == 0)
                {
                    _waitingForSpeedInput = false;
                    ModDisplay.Instance?.HideInput();
                    return true;
                }

                if (int.TryParse(_speedInputBuffer, out int percent) && percent > 0)
                {
                    if (!GS.unlimitedSpeed && percent > 10000)
                    {
                        _waitingForSpeedInput = false;
                        ModDisplay.Instance?.HideInput();
                        return true;
                    }

                    float newSpeed = percent / 100f;
                    SpeedMultiplier = newSpeed;

                    if (ModDisplay.Instance != null && GS.displayStyle != 2)
                    {
                        ModDisplay.Instance.Display($"Set speed to {percent}%");
                    }
                }

                _waitingForSpeedInput = false;
                ModDisplay.Instance?.HideInput();
                return true;
            }

            for (KeyCode key = KeyCode.Alpha0; key <= KeyCode.Alpha9; key++)
            {
                if (Input.GetKeyDown(key))
                {
                    _speedInputBuffer += (char)('0' + (key - KeyCode.Alpha0));
                    if (ModDisplay.Instance != null && GS.displayStyle != 2)
                    {
                        ModDisplay.Instance.DisplayInput("SetGameSpeed: " + _speedInputBuffer);
                    }
                    return true;
                }
            }
            for (KeyCode key = KeyCode.Keypad0; key <= KeyCode.Keypad9; key++)
            {
                if (Input.GetKeyDown(key))
                {
                    _speedInputBuffer += (char)('0' + (key - KeyCode.Keypad0));
                    if (ModDisplay.Instance != null && GS.displayStyle != 2)
                    {
                        ModDisplay.Instance.DisplayInput("SetGameSpeed: " + _speedInputBuffer);
                    }
                    return true;
                }
            }

            return true;
        }

        private void UpdateSpeedDisplay()
        {
            if (GS.displayStyle == 2 || !GS.globalSwitch) return;
            if (ModDisplay.Instance == null)
            {
                ModDisplay.Instance = new ModDisplay();
            }

            string speedString = GS.displayStyle == 0
                ? SpeedMultiplier.ToString(SpeedMultiplier >= 10f ? "00.00" : "0.00", CultureInfo.InvariantCulture)
                : (Math.Round(SpeedMultiplier * 100)).ToString("0.##\\%");
            ModDisplay.Instance.Display($"Game Speed: {speedString}");
        }

        private void StartSpeedUpRebind()
        {
            _speedUpMenuValues[0] = "Set Key...";
            UpdateMenuValueDisplay($"Speed Game Up (+{GS.step})", _speedUpMenuValues[0]);
            _waitingForSpeedUpRebind = true;
            _currentSpeedUpKeyBeforeRebind = _speedUpKeybind;
            if (ModDisplay.Instance != null && GS.displayStyle != 2)
            {
                ModDisplay.Instance.Display("Press a key for Speed Up (Esc to cancel)");
            }
        }

        private bool HandleSpeedUpRebind()
        {
            if (!_waitingForSpeedUpRebind)
            {
                return false;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(key)) continue;

                if (key == KeyCode.Escape)
                {
                    _waitingForSpeedUpRebind = false;
                    return true;
                }

                if (key == _currentSpeedUpKeyBeforeRebind)
                {
                    GS.speedUpKeybind = string.Empty;
                }
                else
                {
                    string keyName = key.ToString();
                    if (IsKeyInUse(keyName, except: "up"))
                        GS.speedUpKeybind = string.Empty;
                    else
                        GS.speedUpKeybind = keyName;
                }

                RefreshKeybinds();
                _waitingForSpeedUpRebind = false;

                string prev = _speedUpMenuValues[0];
                _speedUpMenuValues[0] = FormatKeyLabel(GS.speedUpKeybind);
                UpdateMenuValueDisplay($"Speed Game Up (+{GS.step})", _speedUpMenuValues[0], prev);

                if (ModDisplay.Instance != null && GS.displayStyle != 2)
                {
                    ModDisplay.Instance.Display(string.IsNullOrEmpty(GS.speedUpKeybind)
                        ? "Speed Up key cleared"
                        : $"Speed Up key set to {GS.speedUpKeybind}");
                }

                return true;
            }

            return true;
        }

        private void StartSpeedDownRebind()
        {
            _speedDownMenuValues[0] = "Set Key...";
            UpdateMenuValueDisplay($"Slow Game Down (-{GS.step})", _speedDownMenuValues[0]);
            _waitingForSpeedDownRebind = true;
            _currentSpeedDownKeyBeforeRebind = _slowDownKeybind;
            if (ModDisplay.Instance != null && GS.displayStyle != 2)
            {
                ModDisplay.Instance.Display("Press a key for Slow Down (Esc to cancel)");
            }
        }

        private bool HandleSpeedDownRebind()
        {
            if (!_waitingForSpeedDownRebind)
            {
                return false;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(key)) continue;

                if (key == KeyCode.Escape)
                {
                    _waitingForSpeedDownRebind = false;
                    return true;
                }

                if (key == _currentSpeedDownKeyBeforeRebind)
                {
                    GS.slowDownKeybind = string.Empty;
                }
                else
                {
                    string keyName = key.ToString();
                    if (IsKeyInUse(keyName, except: "down"))
                        GS.slowDownKeybind = string.Empty;
                    else
                        GS.slowDownKeybind = keyName;
                }

                RefreshKeybinds();
                _waitingForSpeedDownRebind = false;

                string prev = _speedDownMenuValues[0];
                _speedDownMenuValues[0] = FormatKeyLabel(GS.slowDownKeybind);
                UpdateMenuValueDisplay($"Slow Game Down (-{GS.step})", _speedDownMenuValues[0], prev);

                if (ModDisplay.Instance != null && GS.displayStyle != 2)
                {
                    ModDisplay.Instance.Display(string.IsNullOrEmpty(GS.slowDownKeybind)
                        ? "Slow Down key cleared"
                        : $"Slow Down key set to {GS.slowDownKeybind}");
                }

                return true;
            }

            return true;
        }

        private class RebindListener : MonoBehaviour
        {
            public Action Tick;

            private void Update()
            {
                Tick?.Invoke();
            }
        }

        public void Unload()
        {
            if (!_isLoaded) return;
            _isLoaded = false;

            if (ModDisplay.Instance != null)
            {
                ModDisplay.Instance.Destroy();
                ModDisplay.Instance = null;
            }

            if (_coroutineHooks != null)
            {
                foreach (ILHook hook in _coroutineHooks)
                    hook.Dispose();
            }

            ModHooks.HeroUpdateHook -= Update;

            if (Time.timeScale != 0)
            {
                Time.timeScale = 1;
            }
        }
    }
}
