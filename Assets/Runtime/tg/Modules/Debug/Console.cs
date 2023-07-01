
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.InputSystem;

using Unity.Entities;

namespace tg.debug
{
    using tg.application.entities;
    using tg.events;
    using tg.ui.events;

    [AttributeUsage(AttributeTargets.Method)]
    public class ConsoleCommandAttribute : Attribute
    {
        public readonly string name;
        public readonly string help;

        public ConsoleCommandAttribute(string name = "", string help = "")
        {
            this.name = name;
            this.help = help;
        }
    }

    namespace entities
    {
        [UpdateInGroup(typeof(PresentationSystemGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame | ApplicationStateMask.AllowRunWhenMenuOpen | ApplicationStateMask.AllowRunWhenPaused, false)]
        public partial class Console : SystemBase
        {
            private struct Command
            {
                public struct ParseResult
                {
                    public bool     hasError;
                    public string   result;

                    public static ParseResult success
                    {
                        get { return new ParseResult { hasError = false, result = "" }; }
                    }

                    public static ParseResult error(string message)
                    {
                        return new ParseResult
                        {
                            hasError    = true,
                            result      = message
                        };
                    }
                }

                public readonly string      name;
                public readonly string      help;
                public readonly MethodInfo  info;

                public Command(MethodInfo info)
                {
                    var attribute = info.GetCustomAttribute<ConsoleCommandAttribute>();

                    this.name = string.IsNullOrEmpty(attribute.name) ? info.Name : attribute.name;
                    this.help = attribute.help;
                    this.info = info;
                }

                public ParseResult parse(string paramStr, out object[] paramOut)
                {
                    var parameter = this.info.GetParameters();
                    var numParams = parameter.Length;

                    paramOut = numParams > 0 ? new object[numParams] : null;

                    if(numParams == 0) { return ParseResult.success; }

                    var paramIn =  Regex.Split(paramStr, @"\s+(?=(?:[^""]*""[^""]*"")*[^""]*$)");

                    for(int i = 0; i < numParams; i++)
                    {
                        if(i < paramIn.Length)
                        {
                            var param = paramIn[i].Trim();
                            switch(Type.GetTypeCode(parameter[i].ParameterType))
                            {
                                case TypeCode.Boolean:
                                {
                                    if(!bool.TryParse(param, out bool result))
                                    {
                                        return ParseResult.error($"Failed to parse parameter '{parameter[i].Name}' = '{param}'. Expected '{parameter[i].ParameterType.Name}' type value.");
                                    }
                                    paramOut[i] = result;
                                    break;
                                }

                                case TypeCode.Single:
                                case TypeCode.Double:
                                case TypeCode.Decimal:
                                {
                                    if(!double.TryParse(param, out double result))
                                    {
                                        return ParseResult.error($"Failed to parse parameter '{parameter[i].Name}' = '{param}'. Expected '{parameter[i].ParameterType.Name}' type value.");
                                    }
                                    paramOut[i] = result;
                                    break;
                                }

                                case TypeCode.SByte:
                                case TypeCode.Int16:
                                case TypeCode.Int32:
                                case TypeCode.Int64:
                                {
                                    if(!long.TryParse(param, out long result))
                                    {
                                        return ParseResult.error($"Failed to parse parameter '{parameter[i].Name}' = '{param}'. Expected '{parameter[i].ParameterType.Name}' type value.");
                                    }
                                    paramOut[i] = result;
                                    break;
                                }

                                case TypeCode.Byte:
                                case TypeCode.UInt16:
                                case TypeCode.UInt32:
                                case TypeCode.UInt64:
                                {
                                    if(!ulong.TryParse(param, out ulong result))
                                    {
                                        return ParseResult.error($"Failed to parse parameter '{parameter[i].Name}' = '{param}'. Expected '{parameter[i].ParameterType.Name}' type value.");
                                    }
                                    paramOut[i] = result;
                                    break;
                                }

                                case TypeCode.String:
                                {
                                    if(param.Contains(" "))
                                    {
                                        // trim quotes
                                        param = param.Substring(1, param.Length - 2);
                                    }

                                    paramOut[i] = param;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if(!parameter[i].HasDefaultValue)
                            {
                                return ParseResult.error("Insufficient parameters!");
                            }

                            paramOut[i] = parameter[i].DefaultValue;
                        }
                    }

                    return ParseResult.success;
                }

                public string generateHelp(bool detailed = false)
                {
                    string buffer = string.IsNullOrEmpty(this.help) ? "No help text provided." : this.help;

                    if(detailed)
                    {
                        buffer += "\n";
                        buffer += this.name;
                        foreach(var param in info.GetParameters())
                        {
                            var paramInfo = $"{param.Name}:{param.ParameterType.Name}";
                            if(param.HasDefaultValue)
                            {
                                paramInfo = $"[{paramInfo}={param.DefaultValue}]";
                            }

                            buffer += $" {paramInfo}";
                        }
                    }

                    return buffer;
                }
            }

            public struct CommandInvocationResult
            {
                public bool     hasError;
                public string[] result;

                public static CommandInvocationResult success(string[] result)
                {
                    return new CommandInvocationResult { hasError = false, result = result };
                }

                public static CommandInvocationResult success(string result)
                {
                    return new CommandInvocationResult { hasError = false, result = new string[] { result } };
                }

                public static CommandInvocationResult error(string[] message)
                {
                    return new CommandInvocationResult
                    {
                        hasError    = true,
                        result      = message
                    };
                }

                public static CommandInvocationResult error(string message)
                {
                    return new CommandInvocationResult
                    {
                        hasError    = true,
                        result      = new string[] { message }
                    };
                }
            }

            public static CommandInvocationResult invokeCommand(string cmdStr)
            {
                var nameAndParams = cmdStr.Split(' ', 2);

                if(cmds.TryGetValue(nameAndParams[0], out Command cmd))
                {
                    var parseResult = cmd.parse(nameAndParams.Length > 1 ? nameAndParams[1] : "", out object[] cmdParams);
                    if(parseResult.hasError)
                    {
                        return CommandInvocationResult.error($"'{nameAndParams[0]}' failed. {parseResult.result}");
                    }

                    try
                    {
                        var results = cmd.info.Invoke(null, cmdParams);
                        if(cmd.info.ReturnType == typeof(void))
                        {
                            return CommandInvocationResult.success("");
                        }
                        else
                        {
                            if(cmd.info.ReturnType.GetInterfaces().Contains(typeof(IEnumerable)))
                            {
                                IList<string> buffer = new List<string>();
                                foreach(var e in results as IEnumerable) { buffer.Add($"{e}"); }

                                return CommandInvocationResult.success(buffer.ToArray());
                            }
                            else
                            {
                                return CommandInvocationResult.success($"{results}");
                            }
                        }
                    }
                    catch(System.Exception ex)
                    {
                        return CommandInvocationResult.error($"'{nameAndParams[0]}' failed. {ex.Message}.");
                    }
                }
                else
                {
                    return CommandInvocationResult.error($"'{nameAndParams[0]}' command does not exist.");
                }
            }

            private static Dictionary<string, Command> cmds;

            public static bool isBusy { get; private set; } = false;

            private InputAction consoleAction;

            static void fetchConsoleCommands()
            {
                if(isBusy) { return; }

                isBusy = true;

                var t0 = DateTime.Now;
                Task.Run(() =>
                {
                    cmds = new Dictionary<string, Command>(16);

                    // Get event types from all currently referenced assemblies.
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var methods = asm
                            .GetTypes()
                            .Where(T => T.IsClass)
                            .SelectMany(C => C.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                            .Where(M => M.GetCustomAttribute<ConsoleCommandAttribute>() != null);
                    
                        foreach(var method in methods)
                        {
                            var cmd = new Command(method);

                            if(!Console.cmds.TryAdd(cmd.name, cmd))
                            {
                                Debug.LogError($"Duplicate console command '${cmd.name}' ({method.DeclaringType.FullName}.{method.Name}) found.");
                            }
                        }
                    }

                    isBusy = false;
                });
            }

            [ConsoleCommand(help: "Shows a list of all avilable command or provides details about a specific command.")]
            static string[] help(string command = "")
            {
                if(!string.IsNullOrEmpty(command))
                {
                    if(cmds.TryGetValue(command, out Command cmd))
                    {
                        return cmd.generateHelp(true).Split('\n');
                    }
                    else
                    {
                        return new string[] { $"'{command}' command does not exist." };
                    }
                }

                var allCmds = cmds.Keys.ToList();
                allCmds.Sort();
                
                return allCmds.Select(c => $"{c} - {cmds[c].help}").ToArray();
            }

            protected override void OnCreate()
            {
                fetchConsoleCommands();

                this.RequireForUpdate(StateManager.state(this));
            }

            protected override void OnStartRunning()
            {
                this.consoleAction = SystemAPI.ManagedAPI.GetSingleton<ApplicationData>().inputActions.FindActionMap("Debug").FindAction("console");
            }

            protected override void OnUpdate()
            {
                if(this.consoleAction.WasPerformedThisFrame())
                {
                    EventQueue.publish(new ToggleViewEvent { name = "console" });
                }
            }
        }
    }
}
