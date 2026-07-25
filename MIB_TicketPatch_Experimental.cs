using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace MIBTicketPatch
{
    [BepInPlugin("br.daniel.mib.ticketpatch", "MIB Ticket Patch Experimental", "0.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource LogSource;
        internal static string OwnLogPath;
        private Harmony _harmony;

        private void Awake()
        {
            LogSource = Logger;
            OwnLogPath = Path.Combine(Paths.GameRootPath, "MIB_TicketPatch.log");
            WriteOwnLog("==================================================");
            WriteOwnLog("MIB Ticket Patch Experimental 0.2.0 iniciado");

            _harmony = new Harmony("br.daniel.mib.ticketpatch");
            int patched = 0;
            patched += PatchSetTickets(_harmony);
            patched += PatchTicketsWon(_harmony);
            patched += PatchDoTickets(_harmony);
            WriteOwnLog("Total de métodos de ticket interceptados: " + patched);
        }

        private static int PatchSetTickets(Harmony harmony)
        {
            MethodInfo target = FindMethod("RESULTS", "SetTickets", new[] { typeof(int), typeof(int) });
            if (target == null)
            {
                WriteOwnLog("ERRO: RESULTS.SetTickets(int, int) não encontrado.");
                return 0;
            }
            try
            {
                MethodInfo prefix = AccessTools.Method(typeof(SetTicketsPatch), nameof(SetTicketsPatch.Prefix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                WriteOwnLog("Patch aplicado: RESULTS.SetTickets(int index, int ticks)");
                return 1;
            }
            catch (Exception ex)
            {
                WriteOwnLog("ERRO ao aplicar patch em RESULTS.SetTickets: " + ex);
                return 0;
            }
        }

        private static int PatchTicketsWon(Harmony harmony)
        {
            MethodInfo target = FindMethodByName("MIB.MIBClient", "TicketsWon");
            if (target == null)
            {
                WriteOwnLog("ERRO: MIB.MIBClient.TicketsWon não encontrado.");
                return 0;
            }
            try
            {
                MethodInfo prefix = AccessTools.Method(typeof(TicketsWonPatch), nameof(TicketsWonPatch.Prefix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                WriteOwnLog("Patch aplicado: " + DescribeMethod(target));
                return 1;
            }
            catch (Exception ex)
            {
                WriteOwnLog("ERRO ao aplicar patch em MIB.MIBClient.TicketsWon: " + ex);
                return 0;
            }
        }

        private static int PatchDoTickets(Harmony harmony)
        {
            MethodInfo target = FindMethodByName("AttractSplashBanner", "DoTickets");
            if (target == null)
            {
                WriteOwnLog("AVISO: AttractSplashBanner.DoTickets não encontrado.");
                return 0;
            }
            try
            {
                MethodInfo prefix = AccessTools.Method(typeof(DoTicketsPatch), nameof(DoTicketsPatch.Prefix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                WriteOwnLog("Patch aplicado: " + DescribeMethod(target));
                return 1;
            }
            catch (Exception ex)
            {
                WriteOwnLog("ERRO ao aplicar patch em AttractSplashBanner.DoTickets: " + ex);
                return 0;
            }
        }

        private static MethodInfo FindMethod(string fullTypeName, string methodName, Type[] parameterTypes)
        {
            Type type = FindType(fullTypeName);
            if (type == null) return null;
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, parameterTypes, null);
        }

        private static MethodInfo FindMethodByName(string fullTypeName, string methodName)
        {
            Type type = FindType(fullTypeName);
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => string.Equals(m.Name, methodName, StringComparison.Ordinal));
        }

        private static Type FindType(string fullTypeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(fullTypeName, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        internal static void WriteOwnLog(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + message;
            try { LogSource?.LogInfo(message); } catch { }
            try { File.AppendAllText(OwnLogPath, line + Environment.NewLine); } catch { }
        }

        internal static string DescribeMethod(MethodBase method)
        {
            if (method == null) return "<método desconhecido>";
            string typeName = method.DeclaringType != null ? method.DeclaringType.FullName : "<tipo desconhecido>";
            string parameters;
            try
            {
                parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name).ToArray());
            }
            catch { parameters = "?"; }
            return typeName + "." + method.Name + "(" + parameters + ")";
        }

        internal static string DumpObject(object value)
        {
            if (value == null) return "<null>";
            try
            {
                Type type = value.GetType();
                var parts = new List<string> { "Tipo=" + type.FullName, "ToString=" + SafeToString(value) };
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object fieldValue = null;
                    try { fieldValue = field.GetValue(value); } catch { }
                    parts.Add("Campo " + field.Name + "=" + FormatValue(fieldValue));
                }
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                    try { parts.Add("Propriedade " + property.Name + "=" + FormatValue(property.GetValue(value, null))); } catch { }
                }
                return string.Join(" | ", parts.ToArray());
            }
            catch (Exception ex) { return "<erro ao inspecionar: " + ex.Message + ">"; }
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "<null>";
            if (value is string) return "\"" + value + "\"";
            if (value is IEnumerable enumerable && !(value is string))
            {
                var items = new List<string>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    items.Add(SafeToString(item));
                    if (++count >= 20) { items.Add("..."); break; }
                }
                return value.GetType().Name + "{" + string.Join(", ", items.ToArray()) + "}";
            }
            return value.GetType().Name + ":" + SafeToString(value);
        }

        private static string SafeToString(object value)
        {
            if (value == null) return "<null>";
            try { return value.ToString(); } catch { return value.GetType().FullName; }
        }
    }

    internal static class SetTicketsPatch
    {
        public static void Prefix(int index, int ticks)
        {
            Plugin.WriteOwnLog("[SET_TICKETS] index=" + index + " | ticks=" + ticks);
        }
    }

    internal static class TicketsWonPatch
    {
        public static void Prefix(MethodBase __originalMethod, object[] __args)
        {
            Plugin.WriteOwnLog("[TICKETS_WON] Método=" + Plugin.DescribeMethod(__originalMethod));
            if (__args == null || __args.Length == 0)
            {
                Plugin.WriteOwnLog("[TICKETS_WON] Nenhum argumento recebido.");
                return;
            }
            for (int i = 0; i < __args.Length; i++)
                Plugin.WriteOwnLog("[TICKETS_WON] Arg" + i + " => " + Plugin.DumpObject(__args[i]));
        }
    }

    internal static class DoTicketsPatch
    {
        public static void Prefix(MethodBase __originalMethod, object[] __args)
        {
            string args = __args == null ? "<null>" : string.Join(", ", __args.Select(a => a == null ? "<null>" : a.GetType().Name + ":" + a).ToArray());
            Plugin.WriteOwnLog("[DO_TICKETS] Método=" + Plugin.DescribeMethod(__originalMethod) + " | Args=[" + args + "]");
        }
    }
}
