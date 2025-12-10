using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Linq;

namespace Himii
{
    public class ScriptManager
    {
        // 存储 EntityID -> 脚本实例
        private static Dictionary<ulong, Entity> _instances = new Dictionary<ulong, Entity>();

        // 缓存所有继承自 Entity 的类型: TypeName -> Type
        private static Dictionary<string, Type> _entityClasses = new Dictionary<string, Type>();

        // Store the load context to unload it later
        private static AssemblyLoadContext _loadContext;

        [UnmanagedCallersOnly]
        public static void LoadGameAssembly(IntPtr assemblyPathPtr)
        {
            string assemblyPath = Marshal.PtrToStringUTF8(assemblyPathPtr);
            Console.WriteLine($"[C#] Loading Game Assembly: {assemblyPath}");

            try
            {
                if (_loadContext != null)
                {
                    _instances.Clear();
                    _entityClasses.Clear();
                    _loadContext.Unload();
                    _loadContext = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                _loadContext = new AssemblyLoadContext("GameAssembly", true);

                // Load from stream to avoid locking the file
                using (var stream = File.OpenRead(assemblyPath))
                {
                    Assembly gameAssembly = _loadContext.LoadFromStream(stream);

                     // 扫描 ScriptCore (自身) 和 GameAssembly 中的所有 Entity 子类
                    var assemblies = new List<Assembly> { typeof(ScriptManager).Assembly, gameAssembly };

                    foreach (var asm in assemblies)
                    {
                        foreach (var type in asm.GetTypes())
                        {
                            if (type.IsSubclassOf(typeof(Entity)) && !type.IsAbstract)
                            {
                                // Key 存储为 "Namespace.ClassName"
                                string fullName = type.FullName;
                                _entityClasses[fullName] = type;
                                Console.WriteLine($"[C#] Found Entity Class: {fullName}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[C#] Error loading assembly: {e.Message}");
            }
        }

        [UnmanagedCallersOnly]
        public static bool EntityClassExists(IntPtr classNamePtr)
        {
            string className = Marshal.PtrToStringUTF8(classNamePtr);
            return _entityClasses.ContainsKey(className);
        }

        [UnmanagedCallersOnly]
        public static void OnCreateEntity(ulong entityID, IntPtr classNamePtr)
        {
            string className = Marshal.PtrToStringUTF8(classNamePtr);

            if (_entityClasses.TryGetValue(className, out Type type))
            {
                // 反射创建实例
                var entity = (Entity)Activator.CreateInstance(type);
                entity.ID = entityID;
                _instances[entityID] = entity;
                entity.OnCreate();
            }
            else
            {
                Console.WriteLine($"[C#] Error: Class '{className}' not found!");
            }
        }

        [UnmanagedCallersOnly]
        public static void OnUpdateEntity(ulong entityID, float ts)
        {
            if (_instances.TryGetValue(entityID, out var entity))
            {
                entity.OnUpdate(ts);
            }
        }
    }
}