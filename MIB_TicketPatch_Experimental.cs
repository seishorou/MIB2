using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace MIBTicketPatch
{
    [BepInPlugin("br.daniel.mib.ticketpatch", "MIB Ticket Patch Experimental", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource LogSource;
        private Harmony _harmony;

        private void Awake()
        {
            LogSource = Logger;
            Logger.LogInfo("MIB Ticket Patch Experimental 0.1.0 carregado.");

            _harmony = new Harmony("br.daniel.mib.ticketpatch");

            int patched = PatchCandidateMethods(_harmony);

            Logger.LogInfo("Total de métodos de ticket interceptados: " + patched);
            Logger.LogInfo("Este primeiro build é de diagnóstico: registra chamadas e valores no LogOutput.log.");
        }

        private static int PatchCandidateMethods(Harmony harmony)
        {
            string[] candidateNames =
            {
                "TicketsWon",
                "DoTickets",
                "SetTickets",
                "MIBData_VendTickets",
                "VendTickets"
            };

            MethodInfo prefix = AccessTools.Method(typeof(TicketMethodPatch), nameof(TicketMethodPatch.Prefix));
            MethodInfo postfix = AccessTools.Method(typeof(TicketMethodPatch), nameof(TicketMethodPatch.Postfix));

            int patched = 0;
            var seen = new HashSet<MethodBase>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(
                            BindingFlags.Instance |
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (MethodInfo method in methods)
                    {
                        if (!candidateNames.Any(n =>
                            string.Equals(method.Name, n, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        if (!seen.Add(method))
                            continue;

                        try
                        {
                            harmony.Patch(
                                method,
                                prefix: new HarmonyMethod(prefix),
                                postfix: new HarmonyMethod(postfix));

                            patched++;
                            LogSource.LogInfo(
                                "Patch aplicado: " +
                                type.FullName + "." + method.Name +
                                FormatParameters(method));
                        }
                        catch (Exception ex)
                        {
                            LogSource.LogWarning(
                                "Falha ao aplicar patch em " +
                                type.FullName + "." + method.Name +
                                ": " + ex.Message);
                        }
                    }
                }
            }

            return patched;
        }

        private static string FormatParameters(MethodInfo method)
        {
            try
            {
                return "(" + string.Join(", ",
                    method.GetParameters()
                          .Select(p => p.ParameterType.Name + " " + p.Name)
                          .ToArray()) + ")";
            }
            catch
            {
                return "(?)";
            }
        }
    }

    internal static class TicketMethodPatch
    {
        public static void Prefix(MethodBase __originalMethod, object __instance, object[] __args)
        {
            try
            {
                Plugin.LogSource.LogInfo(
                    "[TICKET PREFIX] " +
                    DescribeMethod(__originalMethod) +
                    " | args=" + DescribeValues(__args) +
                    " | instancia=" + DescribeObject(__instance));
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning("Erro no Prefix de ticket: " + ex.Message);
            }
        }

        public static void Postfix(
            MethodBase __originalMethod,
            object __instance,
            object[] __args,
            object __result)
        {
            try
            {
                Plugin.LogSource.LogInfo(
                    "[TICKET POSTFIX] " +
                    DescribeMethod(__originalMethod) +
                    " | resultado=" + DescribeObject(__result) +
                    " | args=" + DescribeValues(__args) +
                    " | campos=" + ReadLikelyTicketMembers(__instance));
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning("Erro no Postfix de ticket: " + ex.Message);
            }
        }

        private static string DescribeMethod(MethodBase method)
        {
            if (method == null)
                return "<método desconhecido>";

            string typeName = method.DeclaringType != null
                ? method.DeclaringType.FullName
                : "<tipo desconhecido>";

            return typeName + "." + method.Name;
        }

        private static string DescribeValues(object[] values)
        {
            if (values == null)
                return "<null>";

            return "[" + string.Join(", ",
                values.Select(DescribeObject).ToArray()) + "]";
        }

        private static string DescribeObject(object value)
        {
            if (value == null)
                return "<null>";

            try
            {
                if (value is string)
                    return "\"" + value + "\"";

                if (value is IEnumerable enumerable && !(value is string))
                {
                    var items = new List<string>();
                    int count = 0;

                    foreach (object item in enumerable)
                    {
                        items.Add(item == null ? "<null>" : item.ToString());
                        count++;
                        if (count >= 16)
                        {
                            items.Add("...");
                            break;
                        }
                    }

                    return value.GetType().Name + "{" + string.Join(", ", items.ToArray()) + "}";
                }

                return value.GetType().Name + ":" + value;
            }
            catch
            {
                return value.GetType().FullName;
            }
        }

        private static string ReadLikelyTicketMembers(object instance)
        {
            if (instance == null)
                return "<sem instância>";

            string[] keywords =
            {
                "ticket",
                "vend",
                "payout",
                "award",
                "won"
            };

            var values = new List<string>();
            Type type = instance.GetType();

            try
            {
                foreach (FieldInfo field in type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic))
                {
                    if (!keywords.Any(k =>
                        field.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;

                    object value = null;
                    try { value = field.GetValue(instance); } catch { }

                    values.Add("campo " + field.Name + "=" + DescribeObject(value));
                }

                foreach (PropertyInfo property in type.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                        continue;

                    if (!keywords.Any(k =>
                        property.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;

                    object value = null;
                    try { value = property.GetValue(instance, null); } catch { }

                    values.Add("propriedade " + property.Name + "=" + DescribeObject(value));
                }
            }
            catch (Exception ex)
            {
                values.Add("erro=" + ex.Message);
            }

            return values.Count == 0
                ? "<nenhum membro provável encontrado>"
                : string.Join(" | ", values.ToArray());
        }
    }
}
