using System;
using System.Collections.Generic;

namespace Dialogues
{
    public class Blackboard
    {
        // Store everything as a base object
        private readonly Dictionary<Type, object> _modules = new();

        // Register the exact class type
        public void RegisterModule<T>(T module) where T : class
        {
            Type type = typeof(T);
            _modules[type] = module;
        }

        public T GetModule<T>() where T : class
        {
            if (_modules.TryGetValue(typeof(T), out object module)) return module as T;
            throw new Exception($"Module {typeof(T).Name} not found!");
        }
    }
}