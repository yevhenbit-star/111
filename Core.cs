using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(Fisher781.Core), "Fisher781", "0.2.0", "Fisher781")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace Fisher781
{
    public sealed class Core : MelonMod
    {
        private readonly Settings S = new Settings();
        private readonly AutomationEngine Engine = new AutomationEngine();

        private bool _menuOpen;
        private Rect _window = new Rect(70, 55, 650, 780);
        private Vector2 _scroll;
        private float _nextTick;
        private GUIStyle _title, _section, _label, _small, _button, _toggle, _box, _statusGood, _statusBad;
        private Texture2D _bg, _panel, _accent, _buttonTex, _buttonHover, _onTex, _offTex;
        private bool _stylesReady;

        public override void OnInitializeMelon()
        {
            S.Load();
            LoggerInstance.Msg("Fisher781 загружен. Правый Shift — открыть/закрыть меню.");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.RightShift))
                _menuOpen = !_menuOpen;

            if (!S.Master)
                return;

            if (Time.unscaledTime >= _nextTick)
            {
                _nextTick = Time.unscaledTime + Mathf.Clamp(S.TickSeconds, 0.5f, 10f);
                try
                {
                    Engine.Tick(S);
                }
                catch (Exception ex)
                {
                    Engine.LastStatus = "Ошибка цикла: " + ex.GetType().Name;
                    MelonLogger.Warning("[Fisher781] " + ex);
                }
            }
        }

        public override void OnGUI()
        {
            if (!_menuOpen) return;
            EnsureStyles();

            GUI.depth = -5000;
            _window = GUI.Window(781781, _window, DrawWindow, GUIContent.none, _box);
        }

        private void DrawWindow(int id)
        {
            bool changed = false;

            GUI.Label(new Rect(18, 12, 390, 36), "Fisher781", _title);
            GUI.Label(new Rect(430, 18, 195, 28), "RIGHT SHIFT — закрыть", _small);

            GUI.Box(new Rect(16, 52, 618, 2), GUIContent.none, new GUIStyle { normal = { background = _accent } });

            _scroll = GUI.BeginScrollView(new Rect(14, 66, 620, 690), _scroll, new Rect(0, 0, 590, 1140), false, true);
            float y = 0;

            Section("АВТОПИЛОТ", ref y);
            changed |= ToggleRow("Главная автоматизация", "Запускает только выбранные функции ниже.", ref S.Master, ref y);
            FloatRow("Интервал проверки, сек.", ref S.TickSeconds, 0.5f, 10f, ref y);

            GUI.Label(new Rect(14, y, 555, 42),
                "Принцип: никаких бессмертия, бесконечных денег, мгновенного роста или бесплатного товара. " +
                "Fisher781 автоматизирует обычные игровые действия.", _small);
            y += 48;

            Section("ВЫРАЩИВАНИЕ", ref y);
            changed |= ToggleRow("Автоподготовка горшков", "Добавляет грунт только если он есть в инвентаре.", ref S.AutoSoil, ref y);
            changed |= ToggleRow("Автопосадка", "Берёт 1 семя из инвентаря и сажает в свободный горшок.", ref S.AutoPlant, ref y);
            changed |= ToggleRow("Автополив", "Поливает сухие посадки; порог задаётся ниже.", ref S.AutoWater, ref y);
            changed |= ToggleRow("Автосбор урожая", "Собирает только реально созревшие harvestable-объекты.", ref S.AutoHarvest, ref y);

            TextRow("ID семян", ref S.SeedId, "пример: ogkushseed", ref y);
            TextRow("ID грунта", ref S.SoilId, "оставь пустым, если не нужен", ref y);
            FloatRow("Поливать ниже, %", ref S.WaterBelowPercent, 5f, 95f, ref y);

            Section("ПРОИЗВОДСТВО", ref y);
            changed |= ToggleRow("Автоупаковка", "Запускает упаковку, только когда станция сама считает, что всё готово.", ref S.AutoPackage, ref y);
            IntRow("Макс. упаковок за цикл", ref S.MaxPackagesPerTick, 1, 100, ref y);

            Section("ЗАКУПКИ", ref y);
            changed |= ToggleRow("Автозакупка расходников", "Покупает через обычный магазин: списываются деньги и магазинный запас.", ref S.AutoBuy, ref y);
            TextRow("ID расходника №1", ref S.BuyId1, "например ID семян", ref y);
            IntRow("Держать минимум №1", ref S.BuyTarget1, 0, 999, ref y);
            TextRow("ID расходника №2", ref S.BuyId2, "например упаковка", ref y);
            IntRow("Держать минимум №2", ref S.BuyTarget2, 0, 999, ref y);
            TextRow("ID расходника №3", ref S.BuyId3, "например грунт", ref y);
            IntRow("Держать минимум №3", ref S.BuyTarget3, 0, 999, ref y);

            Section("СДЕЛКИ И ДИЛЕРЫ", ref y);
            changed |= ToggleRow("Автопринимать предложения", "Принимает уже появившиеся предложения клиентов.", ref S.AutoAcceptDeals, ref y);
            changed |= ToggleRow("Автосбор денег у дилеров", "Забирает только уже заработанные дилером деньги.", ref S.AutoCollectDealerCash, ref y);
            FloatRow("Забирать от суммы", ref S.DealerCashThreshold, 0f, 10000f, ref y);
            changed |= ToggleRow("Автоснабжение дилеров", "Переносит уже имеющийся упакованный товар из инвентаря дилерам.", ref S.AutoSupplyDealers, ref y);
            IntRow("Цель товара у дилера", ref S.DealerTargetStock, 0, 200, ref y);

            Section("ДИАГНОСТИКА", ref y);
            GUI.Label(new Rect(14, y, 555, 25), "Статус: " + Engine.LastStatus, 
                Engine.LastTickHadAction ? _statusGood : _label);
            y += 28;
            GUI.Label(new Rect(14, y, 555, 22),
                $"За последний цикл: посадка {Engine.Planted}, урожай {Engine.Harvested}, упаковка {Engine.Packaged}, покупки {Engine.Bought}, сделки {Engine.AcceptedDeals}, дилеры {Engine.DealersTouched}",
                _small);
            y += 32;

            if (GUI.Button(new Rect(14, y, 270, 34), "Выполнить цикл сейчас", _button))
            {
                try { Engine.Tick(S); } catch (Exception ex) { Engine.LastStatus = ex.Message; }
            }
            if (GUI.Button(new Rect(300, y, 270, 34), "Сохранить настройки", _button))
            {
                S.Save();
                Engine.LastStatus = "Настройки сохранены";
            }
            y += 44;

            GUI.EndScrollView();

            if (changed) S.Save();

            GUI.DragWindow(new Rect(0, 0, 650, 58));
        }

        private void Section(string text, ref float y)
        {
            GUI.Label(new Rect(8, y, 560, 28), text, _section);
            y += 31;
        }

        private bool ToggleRow(string title, string hint, ref bool value, ref float y)
        {
            bool old = value;
            GUI.Box(new Rect(8, y, 560, 54), GUIContent.none, new GUIStyle { normal = { background = _panel } });
            if (GUI.Button(new Rect(18, y + 13, 58, 28), value ? "ON" : "OFF", value ? _toggle : _button))
                value = !value;
            GUI.Label(new Rect(88, y + 5, 465, 24), title, _label);
            GUI.Label(new Rect(88, y + 27, 465, 20), hint, _small);
            y += 60;
            return old != value;
        }

        private void TextRow(string label, ref string value, string hint, ref float y)
        {
            GUI.Label(new Rect(14, y + 3, 210, 25), label, _label);
            value = GUI.TextField(new Rect(225, y, 335, 28), value ?? "");
            y += 30;
            GUI.Label(new Rect(225, y, 335, 20), hint, _small);
            y += 24;
        }

        private void IntRow(string label, ref int value, int min, int max, ref float y)
        {
            GUI.Label(new Rect(14, y + 3, 290, 25), label, _label);
            string s = GUI.TextField(new Rect(420, y, 140, 28), value.ToString());
            if (int.TryParse(s, out int v)) value = Mathf.Clamp(v, min, max);
            y += 36;
        }

        private void FloatRow(string label, ref float value, float min, float max, ref float y)
        {
            GUI.Label(new Rect(14, y + 3, 290, 25), label, _label);
            string s = GUI.TextField(new Rect(420, y, 140, 28), value.ToString("0.##", CultureInfo.InvariantCulture));
            if (float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                value = Mathf.Clamp(v, min, max);
            y += 36;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _bg = MakeTex(new Color(0.025f, 0.045f, 0.035f, 0.98f));
            _panel = MakeTex(new Color(0.045f, 0.075f, 0.058f, 0.98f));
            _accent = MakeTex(new Color(0.12f, 0.82f, 0.39f, 1f));
            _buttonTex = MakeTex(new Color(0.07f, 0.18f, 0.11f, 1f));
            _buttonHover = MakeTex(new Color(0.10f, 0.29f, 0.16f, 1f));
            _onTex = MakeTex(new Color(0.10f, 0.55f, 0.25f, 1f));
            _offTex = MakeTex(new Color(0.12f, 0.13f, 0.13f, 1f));

            _box = new GUIStyle(GUI.skin.box);
            _box.normal.background = _bg;
            _box.border = new RectOffset(10, 10, 10, 10);

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 27, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.30f, 1f, 0.55f) }
            };
            _section = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.24f, 0.95f, 0.48f) }
            };
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, normal = { textColor = new Color(0.90f, 0.96f, 0.91f) }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, wordWrap = true,
                normal = { textColor = new Color(0.58f, 0.70f, 0.62f) }
            };
            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { background = _buttonTex, textColor = new Color(0.85f, 1f, 0.88f) },
                hover = { background = _buttonHover, textColor = Color.white },
                active = { background = _accent, textColor = Color.black }
            };
            _toggle = new GUIStyle(_button)
            {
                normal = { background = _onTex, textColor = Color.white },
                hover = { background = _accent, textColor = Color.black }
            };
            _statusGood = new GUIStyle(_label)
            {
                normal = { textColor = new Color(0.30f, 1f, 0.55f) }
            };
            _statusBad = new GUIStyle(_label)
            {
                normal = { textColor = new Color(1f, 0.4f, 0.35f) }
            };
        }

        private static Texture2D MakeTex(Color color)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }
    }

    internal sealed class Settings
    {
        public bool Master = false;
        public float TickSeconds = 2.0f;

        public bool AutoSoil = true;
        public bool AutoPlant = true;
        public bool AutoWater = true;
        public bool AutoHarvest = true;
        public string SeedId = "";
        public string SoilId = "";
        public float WaterBelowPercent = 30f;

        public bool AutoPackage = true;
        public int MaxPackagesPerTick = 12;

        public bool AutoBuy = false;
        public string BuyId1 = "";
        public int BuyTarget1 = 20;
        public string BuyId2 = "";
        public int BuyTarget2 = 20;
        public string BuyId3 = "";
        public int BuyTarget3 = 10;

        public bool AutoAcceptDeals = false;
        public bool AutoCollectDealerCash = true;
        public float DealerCashThreshold = 500f;
        public bool AutoSupplyDealers = false;
        public int DealerTargetStock = 20;

        private string FilePath => Path.Combine(Directory.GetCurrentDirectory(), "UserData", "Fisher781.cfg");

        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int p = line.IndexOf('=');
                    if (p <= 0) continue;
                    string k = line.Substring(0, p).Trim();
                    string v = line.Substring(p + 1).Trim();
                    Set(k, v);
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, new[]
                {
                    "# Fisher781 — настройки автоматизации",
                    $"Master={Master}",
                    $"TickSeconds={TickSeconds.ToString(CultureInfo.InvariantCulture)}",
                    $"AutoSoil={AutoSoil}",
                    $"AutoPlant={AutoPlant}",
                    $"AutoWater={AutoWater}",
                    $"AutoHarvest={AutoHarvest}",
                    $"SeedId={SeedId}",
                    $"SoilId={SoilId}",
                    $"WaterBelowPercent={WaterBelowPercent.ToString(CultureInfo.InvariantCulture)}",
                    $"AutoPackage={AutoPackage}",
                    $"MaxPackagesPerTick={MaxPackagesPerTick}",
                    $"AutoBuy={AutoBuy}",
                    $"BuyId1={BuyId1}",
                    $"BuyTarget1={BuyTarget1}",
                    $"BuyId2={BuyId2}",
                    $"BuyTarget2={BuyTarget2}",
                    $"BuyId3={BuyId3}",
                    $"BuyTarget3={BuyTarget3}",
                    $"AutoAcceptDeals={AutoAcceptDeals}",
                    $"AutoCollectDealerCash={AutoCollectDealerCash}",
                    $"DealerCashThreshold={DealerCashThreshold.ToString(CultureInfo.InvariantCulture)}",
                    $"AutoSupplyDealers={AutoSupplyDealers}",
                    $"DealerTargetStock={DealerTargetStock}"
                });
            }
            catch { }
        }

        private void Set(string k, string v)
        {
            bool B() => bool.TryParse(v, out bool b) && b;
            int I(int d) => int.TryParse(v, out int n) ? n : d;
            float F(float d) => float.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) ? n : d;

            switch (k)
            {
                case nameof(Master): Master = B(); break;
                case nameof(TickSeconds): TickSeconds = F(TickSeconds); break;
                case nameof(AutoSoil): AutoSoil = B(); break;
                case nameof(AutoPlant): AutoPlant = B(); break;
                case nameof(AutoWater): AutoWater = B(); break;
                case nameof(AutoHarvest): AutoHarvest = B(); break;
                case nameof(SeedId): SeedId = v; break;
                case nameof(SoilId): SoilId = v; break;
                case nameof(WaterBelowPercent): WaterBelowPercent = F(WaterBelowPercent); break;
                case nameof(AutoPackage): AutoPackage = B(); break;
                case nameof(MaxPackagesPerTick): MaxPackagesPerTick = I(MaxPackagesPerTick); break;
                case nameof(AutoBuy): AutoBuy = B(); break;
                case nameof(BuyId1): BuyId1 = v; break;
                case nameof(BuyTarget1): BuyTarget1 = I(BuyTarget1); break;
                case nameof(BuyId2): BuyId2 = v; break;
                case nameof(BuyTarget2): BuyTarget2 = I(BuyTarget2); break;
                case nameof(BuyId3): BuyId3 = v; break;
                case nameof(BuyTarget3): BuyTarget3 = I(BuyTarget3); break;
                case nameof(AutoAcceptDeals): AutoAcceptDeals = B(); break;
                case nameof(AutoCollectDealerCash): AutoCollectDealerCash = B(); break;
                case nameof(DealerCashThreshold): DealerCashThreshold = F(DealerCashThreshold); break;
                case nameof(AutoSupplyDealers): AutoSupplyDealers = B(); break;
                case nameof(DealerTargetStock): DealerTargetStock = I(DealerTargetStock); break;
            }
        }
    }

    internal sealed class AutomationEngine
    {
        public string LastStatus = "Ожидание";
        public bool LastTickHadAction;
        public int Planted, Harvested, Packaged, Bought, AcceptedDeals, DealersTouched;

        public void Tick(Settings s)
        {
            Planted = Harvested = Packaged = Bought = AcceptedDeals = DealersTouched = 0;
            LastTickHadAction = false;

            object inventory = R.GetSingleton("PlayerScripts.PlayerInventory");

            if (s.AutoBuy && inventory != null)
            {
                Bought += EnsureStock(inventory, s.BuyId1, s.BuyTarget1);
                Bought += EnsureStock(inventory, s.BuyId2, s.BuyTarget2);
                Bought += EnsureStock(inventory, s.BuyId3, s.BuyTarget3);
            }

            if (s.AutoSoil && inventory != null && !string.IsNullOrWhiteSpace(s.SoilId))
                PrepareSoil(inventory, s.SoilId);

            if (s.AutoPlant && inventory != null && !string.IsNullOrWhiteSpace(s.SeedId))
                PlantAvailable(inventory, s.SeedId);

            if (s.AutoWater)
                WaterDryPots(s.WaterBelowPercent / 100f);

            if (s.AutoHarvest)
                HarvestReady();

            if (s.AutoPackage)
                PackageReady(s.MaxPackagesPerTick);

            if (s.AutoAcceptDeals)
                AcceptDeals();

            if (s.AutoCollectDealerCash)
                CollectDealerCash(s.DealerCashThreshold);

            if (s.AutoSupplyDealers && inventory != null)
                SupplyDealers(inventory, s.DealerTargetStock);

            LastTickHadAction = Planted + Harvested + Packaged + Bought + AcceptedDeals + DealersTouched > 0;
            LastStatus = LastTickHadAction ? "Цикл выполнен — были действия" : "Цикл выполнен — действий не требуется";
        }

        private int EnsureStock(object inventory, string id, int target)
        {
            if (string.IsNullOrWhiteSpace(id) || target <= 0) return 0;
            int have = Convert.ToInt32(R.Invoke(inventory, "GetAmountOfItem", id) ?? 0);
            if (have >= target) return 0;
            int need = Math.Min(target - have, 99);

            Type shopType = R.GameType("UI.Shop.ShopInterface");
            if (shopType == null) return 0;

            object shops = R.GetStatic(shopType, "AllShops");
            foreach (object shop in R.Each(shops))
            {
                object listings = R.Get(shop, "Listings");
                foreach (object listing in R.Each(listings))
                {
                    object item = R.Get(listing, "Item");
                    if (!R.ItemId(item).Equals(id, StringComparison.OrdinalIgnoreCase)) continue;

                    int stock = Convert.ToInt32(R.Get(listing, "CurrentStock") ?? 999);
                    bool unlimited = Convert.ToBoolean(R.Get(listing, "IsUnlimitedStock") ?? false);
                    int qty = unlimited ? need : Math.Min(need, Math.Max(0, stock));
                    if (qty <= 0) return 0;

                    object cart = R.Get(shop, "Cart");
                    if (cart == null) return 0;

                    R.Invoke(cart, "AddItem", listing, qty);
                    bool afford = Convert.ToBoolean(R.Invoke(cart, "CanPlayerAffordCart") ?? false);
                    if (!afford)
                    {
                        R.Invoke(cart, "ClearCart");
                        LastStatus = "Автозакупка: недостаточно денег на " + id;
                        return 0;
                    }

                    R.Invoke(cart, "Buy");
                    return qty;
                }
            }
            return 0;
        }

        private void PrepareSoil(object inventory, string soilId)
        {
            Type potType = R.GameType("ObjectScripts.Pot");
            if (potType == null) return;

            foreach (object pot in R.FindAll(potType))
            {
                if (R.Get(pot, "CurrentSoil") != null) continue;
                uint have = Convert.ToUInt32(R.Invoke(inventory, "GetAmountOfItem", soilId) ?? 0u);
                if (have == 0) break;

                object soilDef = FindDefinitionInInventory(inventory, soilId);
                if (soilDef == null) break;

                try
                {
                    R.Invoke(pot, "SetSoil", soilDef);
                    object capObj = R.Get(pot, "SoilCapacity");
                    float cap = capObj == null ? 1f : Convert.ToSingle(capObj);
                    R.Invoke(pot, "SetSoilAmount", cap);
                    R.Invoke(inventory, "RemoveAmountOfItem", soilId, (uint)1);
                    LastTickHadAction = true;
                }
                catch { }
            }
        }

        private void PlantAvailable(object inventory, string seedId)
        {
            Type potType = R.GameType("ObjectScripts.Pot");
            if (potType == null) return;

            foreach (object pot in R.FindAll(potType))
            {
                uint have = Convert.ToUInt32(R.Invoke(inventory, "GetAmountOfItem", seedId) ?? 0u);
                if (have == 0) break;

                object[] args = new object[] { null };
                bool can = R.InvokeBoolWithOut(pot, "CanAcceptSeed", args);
                if (!can) continue;

                try
                {
                    R.Invoke(inventory, "RemoveAmountOfItem", seedId, (uint)1);
                    R.Invoke(pot, "PlantSeed_Server", seedId, 0f);
                    Planted++;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[Fisher781] Посадка: " + ex.GetType().Name);
                }
            }
        }

        private void WaterDryPots(float threshold)
        {
            Type potType = R.GameType("ObjectScripts.Pot");
            if (potType == null) return;

            foreach (object pot in R.FindAll(potType))
            {
                bool hasPlant = Convert.ToBoolean(R.Invoke(pot, "ContainsGrowable") ?? false);
                if (!hasPlant) continue;

                float moisture = Convert.ToSingle(R.Get(pot, "NormalizedMoistureAmount") ?? 1f);
                if (moisture >= threshold) continue;

                float capacity = Convert.ToSingle(R.Get(pot, "MoistureCapacity") ?? 0f);
                if (capacity <= 0f) continue;

                // Полив автоматизирует повторяющееся действие; рост/скорость не изменяются.
                R.Invoke(pot, "SetMoistureAmount", capacity);
                LastTickHadAction = true;
            }
        }

        private void HarvestReady()
        {
            Type hType = R.GameType("Growing.PlantHarvestable");
            if (hType == null) return;

            foreach (object h in R.FindAll(hType))
            {
                if (!R.IsActiveUnityObject(h)) continue;
                try
                {
                    R.Invoke(h, "Harvest", true);
                    Harvested++;
                }
                catch { }
            }
        }

        private void PackageReady(int max)
        {
            Type stationType = R.GameType("ObjectScripts.PackagingStation");
            if (stationType == null) return;

            foreach (object station in R.FindAll(stationType))
            {
                for (int i = 0; i < max; i++)
                {
                    try
                    {
                        Type modeType = R.NestedType(stationType, "EMode");
                        object packageMode = Enum.ToObject(modeType, 0);
                        object state = R.Invoke(station, "GetState", packageMode);
                        if (state == null || Convert.ToInt32(state) != 0) break; // CanBegin == 0
                        R.Invoke(station, "PackSingleInstance");
                        Packaged++;
                    }
                    catch { break; }
                }
            }
        }

        private void AcceptDeals()
        {
            Type customerType = R.GameType("Economy.Customer");
            if (customerType == null) return;

            object customers = R.GetStatic(customerType, "UnlockedCustomers");
            foreach (object c in R.Each(customers))
            {
                if (R.Get(c, "OfferedContractInfo") == null) continue;
                if (R.Get(c, "CurrentContract") != null) continue;

                try
                {
                    R.Invoke(c, "AcceptContractClicked");
                    AcceptedDeals++;
                }
                catch { }
            }
        }

        private void CollectDealerCash(float threshold)
        {
            Type dealerType = R.GameType("Economy.Dealer");
            if (dealerType == null) return;

            object dealers = R.GetStatic(dealerType, "AllPlayerDealers");
            foreach (object d in R.Each(dealers))
            {
                float cash = Convert.ToSingle(R.Get(d, "Cash") ?? 0f);
                if (cash < threshold) continue;
                try
                {
                    R.Invoke(d, "CollectCash");
                    DealersTouched++;
                }
                catch { }
            }
        }

        private void SupplyDealers(object inventory, int targetStock)
        {
            Type dealerType = R.GameType("Economy.Dealer");
            if (dealerType == null) return;

            object dealers = R.GetStatic(dealerType, "AllPlayerDealers");
            foreach (object dealer in R.Each(dealers))
            {
                int current = Convert.ToInt32(R.Invoke(dealer, "GetPackagedProductAmount") ?? 0);
                if (current >= targetStock) continue;

                object slot = FindPackagedProductSlot(inventory);
                if (slot == null) break;
                object item = R.Get(slot, "ItemInstance");
                if (item == null) continue;

                try
                {
                    R.Invoke(dealer, "AddItemToInventory", item);
                    R.Invoke(slot, "ClearStoredInstance", false);
                    DealersTouched++;
                }
                catch { }
            }
        }

        private object FindDefinitionInInventory(object inventory, string id)
        {
            object slots = R.Invoke(inventory, "GetAllInventorySlots");
            foreach (object slot in R.Each(slots))
            {
                object item = R.Get(slot, "ItemInstance");
                if (item == null) continue;
                object def = R.Get(item, "Definition") ?? R.Get(item, "ItemDefinition");
                if (def != null && R.ItemId(def).Equals(id, StringComparison.OrdinalIgnoreCase))
                    return def;
            }
            return null;
        }

        private object FindPackagedProductSlot(object inventory)
        {
            object slots = R.Invoke(inventory, "GetAllInventorySlots");
            foreach (object slot in R.Each(slots))
            {
                object item = R.Get(slot, "ItemInstance");
                if (item == null) continue;
                string n = item.GetType().FullName ?? item.GetType().Name;
                if (!n.Contains("ProductItemInstance")) continue;

                object packaging = R.Get(item, "Packaging") ?? R.Get(item, "PackagingDefinition") ?? R.Get(item, "PackagingID");
                if (packaging != null) return slot;
            }
            return null;
        }
    }

    internal static class R
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        public static Type GameType(string suffix)
        {
            string[] candidates =
            {
                "Il2CppScheduleOne." + suffix,
                "ScheduleOne." + suffix
            };

            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string name in candidates)
                {
                    Type t = a.GetType(name, false);
                    if (t != null) return t;
                }
            }
            return null;
        }

        public static Type NestedType(Type parent, string name) =>
            parent?.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);

        public static object GetSingleton(string suffix)
        {
            Type t = GameType(suffix);
            if (t == null) return null;
            return GetStatic(t, "Instance") ?? GetStatic(t, "instance");
        }

        public static object GetStatic(Type t, string name)
        {
            if (t == null) return null;
            PropertyInfo p = t.GetProperty(name, AllStatic);
            if (p != null) return p.GetValue(null);
            FieldInfo f = t.GetField(name, AllStatic);
            if (f != null) return f.GetValue(null);
            return null;
        }

        public static object Get(object o, string name)
        {
            if (o == null) return null;
            Type t = o.GetType();

            PropertyInfo p = t.GetProperty(name, AllInstance);
            if (p != null)
            {
                try { return p.GetValue(o); } catch { }
            }

            FieldInfo f = t.GetField(name, AllInstance);
            if (f != null)
            {
                try { return f.GetValue(o); } catch { }
            }
            return null;
        }

        public static object Invoke(object o, string name, params object[] args)
        {
            if (o == null) return null;
            Type t = o.GetType();
            MethodInfo method = FindMethod(t, name, args?.Length ?? 0);
            if (method == null) return null;

            try { return method.Invoke(o, args); }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }

        public static bool InvokeBoolWithOut(object o, string name, object[] args)
        {
            object result = Invoke(o, name, args);
            return result is bool b && b;
        }

        private static MethodInfo FindMethod(Type t, string name, int argc)
        {
            for (Type cur = t; cur != null; cur = cur.BaseType)
            {
                MethodInfo m = cur.GetMethods(AllInstance)
                    .FirstOrDefault(x => x.Name == name && x.GetParameters().Length == argc);
                if (m != null) return m;
            }
            return null;
        }

        public static IEnumerable<object> Each(object collection)
        {
            if (collection == null) yield break;

            if (collection is IEnumerable e)
            {
                foreach (object x in e) if (x != null) yield return x;
                yield break;
            }

            object countObj = Get(collection, "Count") ?? Get(collection, "Length");
            if (countObj == null) yield break;
            int count = Convert.ToInt32(countObj);

            MethodInfo getter = collection.GetType().GetMethod("get_Item", AllInstance);
            if (getter == null) yield break;

            for (int i = 0; i < count; i++)
            {
                object x = null;
                try { x = getter.Invoke(collection, new object[] { i }); } catch { }
                if (x != null) yield return x;
            }
        }

        private static object ToIl2CppType(Type wrapperType)
        {
            if (wrapperType == null) return null;
            try
            {
                Type helper = Type.GetType("Il2CppInterop.Runtime.Il2CppType, Il2CppInterop.Runtime", false);
                if (helper == null) return null;

                MethodInfo of = helper.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Of" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                if (of == null) return null;

                return of.MakeGenericMethod(wrapperType).Invoke(null, null);
            }
            catch { return null; }
        }

        public static IEnumerable<object> FindAll(Type t)
        {
            if (t == null) yield break;

            object ilType = ToIl2CppType(t);
            if (ilType == null) yield break;

            object arr = null;
            try
            {
                MethodInfo find = typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "FindObjectsOfTypeAll" && !m.IsGenericMethod && m.GetParameters().Length == 1);
                if (find != null) arr = find.Invoke(null, new[] { ilType });
            }
            catch { }

            foreach (object o in Each(arr))
                if (o != null) yield return o;
        }

        public static string ItemId(object item)
        {
            if (item == null) return "";
            foreach (string name in new[] { "ID", "Id", "id", "ItemID", "Name", "name" })
            {
                object v = Get(item, name);
                if (v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                    return v.ToString();
            }
            return item.ToString() ?? "";
        }

        public static bool IsActiveUnityObject(object o)
        {
            if (o == null) return false;
            try
            {
                object go = Get(o, "gameObject");
                if (go == null) return true;
                object active = Get(go, "activeInHierarchy");
                return active == null || Convert.ToBoolean(active);
            }
            catch { return true; }
        }
    }
}
