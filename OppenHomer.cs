using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using Model;
using Model.Entities;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace OppenHomer
{
    [BepInPlugin("OppenHomer", "OppenHomer", "1.0.0")]
    public class OppenHomerPlugin : BaseUnityPlugin
    {
        // ==================== SINGLETON Y LOGGING ====================
        public static OppenHomerPlugin Instance { get; private set; }
        public static ManualLogSource ModLogger { get; private set; }

        // ==================== CONFIGURACIONES ====================
        public static ConfigEntry<bool> EnableMod;
        public static ConfigEntry<float> MinWallEnclosurePercent;
        public static ConfigEntry<bool> RequireRoof;
        public static ConfigEntry<bool> EnableDebugLogs;
        public static ConfigEntry<float> RaycastDistance;

        // ==================== ESTADO VANILLA CAPTURADO ====================
        private static bool _vanillaIsUnderRoof = false;
        private static float _currentVanillaCoverPercent = 0f;

        // ==================== INICIALIZACIÓN ====================
        void Awake()
        {
            Instance = this;
            ModLogger = Logger;
            SetupConfig();

            var harmony = new Harmony("OppenHomer");
            harmony.PatchAll();

            ModLogger.LogInfo($"OppenHomer v1.0 initialized - Unity Native + Complementary Logic!");
            ModLogger.LogInfo($"🏠 PHILOSOPHY: Opening new home possibilities with natural barriers!");
        }

        private void SetupConfig()
        {
            EnableMod = Config.Bind("General", "EnableMod", true,
                "Enable/disable OppenHomer completely");

            MinWallEnclosurePercent = Config.Bind("Detection", "MinWallEnclosurePercent", 75.0f,
                "Minimum wall enclosure percentage (50-90%). 75% = max 25% openings allowed. Uses vanilla's 26-direction system");

            RequireRoof = Config.Bind("Detection", "RequireRoof", true,
                "Require roof coverage for home detection. TRUE: Must have roof | FALSE: Allow open patios");

            RaycastDistance = Config.Bind("Detection", "RaycastDistance", 12.0f,
                "Distance for wall detection raycast (8-20m). Increased for better terrain detection");

            EnableDebugLogs = Config.Bind("Debug", "EnableDebugLogs", false,
                "Enable detailed debug logging. WARNING: Very verbose output");
        }

        // ==================== PATCH PRINCIPAL - CORAZÓN DEL MOD ====================
        /// <summary>
        /// Patch principal - Intercepta el cálculo de estado de hogar de vanilla
        /// Filosofía: Solo actuar cuando vanilla detecta construcción parcial pero la rechaza
        /// </summary>
        [HarmonyPatch(typeof(PlayerStatuses), "HouseStatusIndex")]
        public static class PlayerStatuses_HouseStatusIndex_Patch
        {
            static void Postfix(float coverPercent, float coverQuality, bool isUnderRoof, ref int __result)
            {
                if (!EnableMod.Value) return;

                try
                {
                    // Capturar estado vanilla para lógica complementaria
                    _vanillaIsUnderRoof = isUnderRoof;
                    _currentVanillaCoverPercent = coverPercent;

                    // Si vanilla ya aprobó la casa, no interferir
                    if (__result >= 1)
                    {
                        if (EnableDebugLogs.Value)
                            ModLogger.LogInfo($"✅ Vanilla approved house (level {__result}), no action needed");
                        return;
                    }

                    // LÓGICA CLAVE: Solo actuar cuando vanilla detecta construcción pero la rechaza
                    bool vanillaDetectsConstruction = coverPercent > 0f || isUnderRoof;

                    if (!vanillaDetectsConstruction)
                    {
                        if (EnableDebugLogs.Value)
                            ModLogger.LogInfo($"🔍 No construction detected by vanilla (cover: {coverPercent:F1}%, roof: {isUnderRoof}) - mod won't act");
                        return;
                    }

                    ModLogger.LogInfo($"🔍 Vanilla detected construction but rejected it - checking if natural barriers can complete it...");
                    ModLogger.LogInfo($"📊 Vanilla values: cover={coverPercent:F1}%, quality={coverQuality:F2}, roof={isUnderRoof}");
                    ModLogger.LogInfo($"🎯 MOD LOGIC: Working continuously with vanilla - checking EVERY time vanilla processes");

                    // Verificar si barreras naturales completan el hogar (siempre fresco, sin cache)
                    bool naturalBarriersCompleteHome = DoNaturalBarriersCompleteHome();

                    if (naturalBarriersCompleteHome)
                    {
                        // Determinar nivel basándose en coverQuality vanilla (respetando su lógica)
                        int targetLevel;
                        if (coverQuality >= 1.5f) // Umbral vanilla House2
                        {
                            targetLevel = 2; // House2 - materiales de alta calidad
                        }
                        else
                        {
                            targetLevel = 1; // House1 - materiales básicos
                        }

                        // Aplicar resultado directamente
                        __result = targetLevel;
                        ModLogger.LogInfo($"✅ Natural barriers complete home! Vanilla quality ({coverQuality:F2}) determines level → House{__result}");
                        ModLogger.LogInfo($"🎯 MOD ROLE: Completed enclosure with natural barriers, vanilla quality sets the level");
                    }
                    else
                    {
                        if (EnableDebugLogs.Value)
                            ModLogger.LogInfo($"❌ Natural barriers don't complete the home");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"❌ Error in HouseStatusIndex patch: {ex.Message}");
                }
            }
        }

        // ==================== LÓGICA DE DETECCIÓN DE HOGAR ====================
        /// <summary>
        /// Verificar si barreras naturales completan el hogar (sin cache - siempre preciso)
        /// </summary>
        private static bool DoNaturalBarriersCompleteHome()
        {
            Vector3 playerPos = GetPlayerPosition();
            if (playerPos == Vector3.zero) return false;

            ModLogger.LogInfo($"🔍 Checking natural barriers at {playerPos} (Unity native detection - no cache)");

            // Verificar componentes del hogar
            bool wallsComplete = CheckWallEnclosure(playerPos);  // 26 direcciones
            bool roofValid = CheckRoof(playerPos);               // 1 raycast vertical

            bool isComplete = wallsComplete && roofValid;

            // SIEMPRE mostrar estos logs para transparencia
            ModLogger.LogInfo($"📊 Home completion check:");
            ModLogger.LogInfo($"   - Walls: {(wallsComplete ? "✅" : "❌")}");
            ModLogger.LogInfo($"   - Roof: {(roofValid ? "✅" : "❌")} (required: {RequireRoof.Value})");
            ModLogger.LogInfo($"   → Result: {(isComplete ? "COMPLETE" : "INCOMPLETE")}");

            return isComplete;
        }

        // ==================== VERIFICACIÓN DE PAREDES (26 DIRECCIONES) ====================
        /// <summary>
        /// Verificar enclosure de paredes - FILOSOFÍA UNIFICADA: Solo barreras naturales (complementario a vanilla)
        /// </summary>
        private static bool CheckWallEnclosure(Vector3 playerPos)
        {
            // Direcciones vanilla exactas (copiadas del sistema original del juego)
            Vector3[] vanillaDirs = new Vector3[]
            {
                new Vector3(1f, 0f, 0f),   new Vector3(-1f, 0f, 0f),  new Vector3(0f, 0f, 1f),    new Vector3(0f, 0f, -1f),
                new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f),  new Vector3(-1f, 0f, 1f),   new Vector3(1f, 0f, 1f),
                new Vector3(0f, 1f, 0f),   new Vector3(-1f, 1f, 0f),  new Vector3(1f, 1f, 0f),    new Vector3(0f, 1f, 1f),
                new Vector3(0f, 1f, -1f),  new Vector3(-1f, 1f, -1f), new Vector3(1f, 1f, -1f),   new Vector3(-1f, 1f, 1f),
                new Vector3(1f, 1f, 1f),   new Vector3(0f, -1f, 0f),  new Vector3(-1f, -1f, 0f),  new Vector3(1f, -1f, 0f),
                new Vector3(0f, -1f, 1f),  new Vector3(0f, -1f, -1f), new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
                new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f)
            };

            int totalDirections = 26;
            int naturalBarriersFound = 0; // Contar SOLO barreras naturales encontradas
            float rayDistance = RaycastDistance.Value;

            ModLogger.LogInfo($"🔍 Checking for NATURAL barriers only (vanilla already counted constructions):");

            for (int i = 0; i < totalDirections; i++)
            {
                Vector3 direction = vanillaDirs[i];

                // Buscar SOLO barreras naturales
                var naturalBarrier = GetNaturalBarrierInDirection(playerPos, direction, rayDistance);

                if (naturalBarrier.HasValue)
                {
                    var hit = naturalBarrier.Value;
                    naturalBarriersFound++;
                    ModLogger.LogInfo($"    Direction {i}: ✅ NATURAL BARRIER - '{hit.collider.name}' (distance: {hit.distance:F1}m)");
                }
                else
                {
                    ModLogger.LogInfo($"    Direction {i}: ❌ NO NATURAL BARRIER (checked {rayDistance}m)");
                }
            }

            // NUEVA LÓGICA: Vanilla + Natural = Total
            // Vanilla ya procesó sus construcciones y dio coverPercent
            // Nosotros añadimos las barreras naturales como complemento

            float naturalBarrierPercent = (float)naturalBarriersFound / totalDirections * 100f;
            float vanillaCoverPercent = _currentVanillaCoverPercent;

            // TOTAL = Vanilla constructions + Natural barriers
            float totalEnclosurePercent = vanillaCoverPercent + naturalBarrierPercent;

            // Limitar al 100% máximo
            if (totalEnclosurePercent > 100f) totalEnclosurePercent = 100f;

            bool isComplete = totalEnclosurePercent >= MinWallEnclosurePercent.Value;

            ModLogger.LogInfo($"📊 UNIFIED LOGIC CALCULATION:");
            ModLogger.LogInfo($"   - Vanilla constructions: {vanillaCoverPercent:F1}%");
            ModLogger.LogInfo($"   - Natural barriers: {naturalBarrierPercent:F1}% ({naturalBarriersFound}/26 directions)");
            ModLogger.LogInfo($"   - TOTAL enclosure: {totalEnclosurePercent:F1}% (need {MinWallEnclosurePercent.Value:F1}%)");
            ModLogger.LogInfo($"   → Result: {(isComplete ? "COMPLETE" : "INCOMPLETE")}");

            return isComplete;
        }

        /// <summary>
        /// Buscar SOLO barreras naturales en una dirección (simplificado)
        /// </summary>
        private static RaycastHit? GetNaturalBarrierInDirection(Vector3 origin, Vector3 direction, float maxDistance)
        {
            Ray ray = new Ray(origin, direction);
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance);

            // Ordenar hits por distancia (más cerca primero)
            Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));

            if (EnableDebugLogs.Value && hits.Length > 1)
                ModLogger.LogInfo($"      🔍 Found {hits.Length} hits, checking each...");

            foreach (var hit in hits)
            {
                // ❌ IGNORAR construcciones del jugador (vanilla ya las procesó)
                if (HasBuildingComponent(hit.collider))
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"        🔍 IGNORING '{hit.collider.name}' at {hit.distance:F1}m - construction (vanilla counted)");
                    continue; // Vanilla ya las contó
                }

                // ✅ BUSCAR SOLO barreras naturales (ya filtra equipamiento internamente)
                if (IsNaturalBarrier(hit.collider.gameObject))
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"        🔍 FOUND natural barrier '{hit.collider.name}' at {hit.distance:F1}m");
                    return hit;
                }
                else
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"        🔍 IGNORING '{hit.collider.name}' at {hit.distance:F1}m - not natural barrier");
                    continue; // Todo lo demás se ignora automáticamente
                }
            }

            if (EnableDebugLogs.Value)
                ModLogger.LogInfo($"        ❌ No natural barriers found in {maxDistance}m");

            return null;
        }

        // ==================== VERIFICACIÓN DE TECHO ====================
        /// <summary>
        /// Verificar techo - LÓGICA COMPLEMENTARIA: Solo natural si vanilla no detectó construido
        /// </summary>
        private static bool CheckRoof(Vector3 playerPos)
        {
            // Si RequireRoof está deshabilitado, usar lógica inteligente
            if (!RequireRoof.Value)
            {
                // VERIFICACIÓN INTELIGENTE: Si vanilla dice roof=False Y estamos comprobando,
                // probablemente sea al aire libre
                if (!_vanillaIsUnderRoof)
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"    Roof: ❌ Vanilla roof=False suggests open air location - being conservative");
                    return false;
                }
                else
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"    Roof: ✅ Vanilla detected roof, allowing (RequireRoof=false)");
                    return true;
                }
            }

            // LÓGICA COMPLEMENTARIA: Vanilla primero, natural después
            if (_vanillaIsUnderRoof)
            {
                // Vanilla ya detectó techo construido - PERFECTO, no buscar más
                if (EnableDebugLogs.Value)
                    ModLogger.LogInfo($"    Roof: ✅ Vanilla detected constructed roof - complementary check passed");
                return true;
            }

            // Vanilla NO detectó techo construido - buscar SOLO techo natural
            if (EnableDebugLogs.Value)
                ModLogger.LogInfo($"    Roof: Vanilla found no constructed roof, checking for NATURAL roof only...");

            return CheckNaturalRoofOnly(playerPos);
        }

        /// <summary>
        /// Buscar SOLO techos naturales (cuando vanilla no encontró construidos)
        /// </summary>
        private static bool CheckNaturalRoofOnly(Vector3 playerPos)
        {
            float rayDistance = 15f; // Mayor distancia para techos altos
            Ray ray = new Ray(playerPos + Vector3.up * 0.5f, Vector3.up);

            // Usar RaycastAll para ignorar equipamiento del jugador
            RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance);
            Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));

            if (EnableDebugLogs.Value)
                ModLogger.LogInfo($"      Natural roof check: Found {hits.Length} potential hits in {rayDistance}m");

            foreach (var hit in hits)
            {
                // LÓGICA COMPLEMENTARIA: Ignorar construcciones del jugador (vanilla ya las procesó)
                if (HasBuildingComponent(hit.collider))
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"        🔍 IGNORING player building '{hit.collider.name}' at {hit.distance:F1}m - vanilla should have detected this");
                    continue; // Vanilla debería haberlo detectado
                }

                // BUSCAR SOLO barreras naturales (ya filtra equipamiento internamente)
                if (IsNaturalBarrier(hit.collider.gameObject))
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"    Roof: ✅ Found NATURAL roof - '{hit.collider.name}' at {hit.distance:F1}m");
                    return true;
                }
                else
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"        🔍 REJECTING roof hit '{hit.collider.name}' at {hit.distance:F1}m - not natural barrier");
                    continue; // Todo lo demás se ignora automáticamente
                }
            }

            if (EnableDebugLogs.Value)
                ModLogger.LogInfo($"    Roof: ❌ No natural roof found in {rayDistance}m - open sky");

            return false;
        }

        // ==================== DETECCIÓN DE BARRERAS NATURALES ====================
        /// <summary>
        /// Detectar barreras naturales usando SOLO Unity nativo (sin palabras clave intuitivas)
        /// Método unificado para paredes, suelos y techos naturales
        /// </summary>
        private static bool IsNaturalBarrier(GameObject obj)
        {
            if (obj == null) return false;

            var collider = obj.GetComponent<Collider>();
            if (collider == null || collider.isTrigger) return false;

            string name = obj.name.ToLower();

            // EXCLUSIONES PRIORITARIAS (equipamiento del jugador y objetos obvios)
            bool isExcluded = name.Contains("ammopicker") || name.Contains("ammo") ||
                             name.Contains("picker") || name.Contains("inventory") ||
                             name.Contains("equipment") || name.Contains("weapon") ||
                             name.Contains("capsule") || name.Contains("player") ||
                             name.Contains("water") || name.Contains("trigger") ||
                             name.Contains("invisible") || name.Contains("effect") ||
                             name.Contains("dissallow") || name.Contains("disallow");

            if (isExcluded)
            {
                if (EnableDebugLogs.Value)
                    ModLogger.LogInfo($"      🔍 '{obj.name}' → ❌ EXCLUDED (known non-terrain)");
                return false;
            }

            // MÉTODO 1: TerrainCollider oficial de Unity
            var terrainCollider = obj.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                if (EnableDebugLogs.Value)
                    ModLogger.LogInfo($"      🔍 '{obj.name}' → ✅ VALID (Unity TerrainCollider)");
                return true;
            }

            // MÉTODO 2: Componente Terrain oficial de Unity
            var terrain = obj.GetComponent<Terrain>();
            if (terrain != null)
            {
                try
                {
                    if (terrain.terrainData != null)
                    {
                        if (EnableDebugLogs.Value)
                            ModLogger.LogInfo($"      🔍 '{obj.name}' → ✅ VALID (Unity Terrain component with TerrainData)");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"      🔍 '{obj.name}' → ❌ Terrain component but invalid TerrainData: {ex.Message}");
                }
            }

            // MÉTODO 3: SOLO objetos estáticos grandes sin Rigidbody (física básica, sin nombres)
            var rb = obj.GetComponent<Rigidbody>();
            bool isStatic = rb == null || rb.isKinematic;

            if (!isStatic)
            {
                if (EnableDebugLogs.Value)
                    ModLogger.LogInfo($"      🔍 '{obj.name}' → ❌ REJECTED (has dynamic Rigidbody - not terrain)");
                return false;
            }

            // Verificar tamaño significativo (física pura - sin nombres)
            var bounds = collider.bounds;
            float size = bounds.size.magnitude;

            if (size > 3f && bounds.size.y > 1f && bounds.size.y < 100f)
            {
                // Verificación MeshCollider
                var meshCollider = collider as MeshCollider;
                if (meshCollider != null)
                {
                    // MeshCollider no-convex típico de terreno
                    if (!meshCollider.convex)
                    {
                        if (EnableDebugLogs.Value)
                            ModLogger.LogInfo($"      🔍 '{obj.name}' → ✅ VALID (large non-convex MeshCollider: {size:F1}m - likely terrain)");
                        return true;
                    }
                    else if (size > 10f) // Convex pero muy grande
                    {
                        if (EnableDebugLogs.Value)
                            ModLogger.LogInfo($"      🔍 '{obj.name}' → ✅ VALID (very large convex MeshCollider: {size:F1}m)");
                        return true;
                    }
                    else
                    {
                        if (EnableDebugLogs.Value)
                            ModLogger.LogInfo($"      🔍 '{obj.name}' → ❌ REJECTED (small convex MeshCollider: {size:F1}m - likely prop)");
                        return false;
                    }
                }
                else
                {
                    // Otros tipos de collider grandes y estáticos
                    if (EnableDebugLogs.Value)
                        ModLogger.LogInfo($"      🔍 '{obj.name}' → ✅ VALID (large static {collider.GetType().Name}: {size:F1}m)");
                    return true;
                }
            }
            else
            {
                if (EnableDebugLogs.Value)
                    ModLogger.LogInfo($"      🔍 '{obj.name}' → ❌ REJECTED (too small or bad dimensions: {size:F1}m, height: {bounds.size.y:F1}m)");
                return false;
            }
        }

        // ==================== MÉTODOS AUXILIARES ====================
        /// <summary>
        /// Obtener posición del jugador usando el mismo método que vanilla
        /// </summary>
        private static Vector3 GetPlayerPosition()
        {
            try
            {
                if (GameManagerAdapter.Alive && GameManagerAdapter.Instance.IsInit)
                {
                    return GameManagerAdapter.Instance.Game.PlayerProperties.HeadPos;
                }
            }
            catch { }

            // Fallback
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            return player?.transform.position ?? Vector3.zero;
        }

        /// <summary>
        /// Obtener GameObject del jugador
        /// </summary>
        private static GameObject GetPlayerGameObject()
        {
            return GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        }

        /// <summary>
        /// Verificar si un collider pertenece a una construcción del jugador
        /// </summary>
        private static bool HasBuildingComponent(Collider collider)
        {
            return collider.GetComponentInParent<BuildingBehaviour>() != null;
        }
    }
}