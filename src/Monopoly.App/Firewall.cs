using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Monopoly.Net;

namespace Monopoly.App
{
    // Брандмауэр Windows: пустит ли он друзей к хосту.
    //  • Allowed — есть разрешающее правило для игры или для порта 7777.
    //  • Blocked — есть блокирующее правило для игры. Windows создаёт его сама, если в её окне
    //    «Разрешить доступ» нажали «Отмена». Блокировка сильнее разрешения — такое правило надо удалить.
    //  • Unknown — правил нет или проверить не удалось.
    public enum FirewallStatus { Allowed, Blocked, Unknown }

    public sealed record FirewallCheck(FirewallStatus Status, IReadOnlyList<string> BlockRuleNames);

    public static class Firewall
    {
        private const string RuleName = "Monopoly Game (7777)";
        private const int DirectionIn = 1;
        private const int ActionBlock = 0;
        private const int ActionAllow = 1;

        private static string ExePath => Environment.ProcessPath ?? "";

        // Читает правила через COM-объект брандмауэра: права администратора не нужны, язык Windows не важен.
        public static FirewallCheck Check()
        {
            try
            {
                var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (type is null)
                {
                    return new FirewallCheck(FirewallStatus.Unknown, Array.Empty<string>());
                }
                dynamic policy = Activator.CreateInstance(type)!;

                bool allowed = false;
                var blockRules = new List<string>();
                foreach (dynamic rule in policy.Rules)
                {
                    if (!(bool)rule.Enabled || (int)rule.Direction != DirectionIn)
                    {
                        continue;
                    }
                    string name = rule.Name ?? "";
                    string? app = rule.ApplicationName;
                    bool forThisGame = string.Equals(app, ExePath, StringComparison.OrdinalIgnoreCase);
                    int action = rule.Action;

                    if (forThisGame && action == ActionBlock)
                    {
                        blockRules.Add(name);
                    }
                    else if (action == ActionAllow && (forThisGame || name == RuleName))
                    {
                        allowed = true;
                    }
                }

                var status = blockRules.Count > 0 ? FirewallStatus.Blocked
                    : allowed ? FirewallStatus.Allowed
                    : FirewallStatus.Unknown;
                return new FirewallCheck(status, blockRules.Distinct().ToList());
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                return new FirewallCheck(FirewallStatus.Unknown, Array.Empty<string>());
            }
        }

        // Удаляет блокирующие правила для игры и открывает порт 7777 (TCP — игра, UDP — поиск).
        // Windows спросит разрешение администратора. Возвращает false, если в окне UAC отказались.
        public static bool TryAllow(IReadOnlyList<string> blockRuleNames)
        {
            var commands = blockRuleNames
                .Select(name => $"netsh advfirewall firewall delete rule name=\"{name}\" dir=in program=\"{ExePath}\"")
                // Своё правило пересоздаём, чтобы повторное нажатие не плодило копии.
                .Append($"netsh advfirewall firewall delete rule name=\"{RuleName}\"")
                .Append(AddRule("TCP"))
                .Append(AddRule("UDP"));
            try
            {
                var info = new ProcessStartInfo("cmd.exe", "/c " + string.Join(" & ", commands))
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using var process = Process.Start(info)!;
                process.WaitForExit();
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }

        private static string AddRule(string protocol) =>
            $"netsh advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol={protocol} localport={NetDefaults.Port} profile=any";
    }
}
