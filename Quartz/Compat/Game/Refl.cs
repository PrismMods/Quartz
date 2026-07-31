using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Quartz.Core;
namespace Quartz.Compat.Game;
public static partial class Refl {
    internal const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    internal sealed class Member {
        private readonly PropertyInfo prop;
        private readonly FieldInfo fieldInfo;
        internal Member(Type owner, params string[] names) {
            if(owner == null) return;
            foreach(string n in names) {
                try {
                    prop = owner.GetProperty(n, Any);
                } catch(AmbiguousMatchException e) {
                    Diag.Ignore(e);
                    prop = Walk(owner, t => t.GetProperty(n, Declared));
                }
                if(prop != null && prop.GetIndexParameters().Length == 0) return;
                prop = null;
                try {
                    fieldInfo = owner.GetField(n, Any);
                } catch(AmbiguousMatchException e) {
                    Diag.Ignore(e);
                    fieldInfo = Walk(owner, t => t.GetField(n, Declared));
                }
                if(fieldInfo != null) return;
            }
        }
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static T Walk<T>(Type owner, Func<Type, T> pick) where T : class {
            for(Type t = owner; t != null; t = t.BaseType) {
                T found = null;
                try {
                    found = pick(t);
                } catch(Exception e) {
                    Diag.Ignore(e);
                }
                if(found != null) return found;
            }
            return null;
        }
        internal bool Exists => prop != null || fieldInfo != null;
        internal object Get(object instance) {
            try {
                if(prop != null) return prop.CanRead ? prop.GetValue(instance, null) : null;
                return fieldInfo?.GetValue(instance);
            } catch(Exception e) {
                Diag.Ignore(e);
                return null;
            }
        }
        internal void Set(object instance, object value) {
            try {
                if(prop != null) {
                    if(prop.CanWrite) prop.SetValue(instance, value, null);
                    return;
                }
                fieldInfo?.SetValue(instance, value);
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        }
        internal T Get<T>(object instance, T fallback = default) =>
            Get(instance) is T t ? t : fallback;
        internal Func<TOwner, TValue> BindGetter<TOwner, TValue>() =>
            Bind<Func<TOwner, TValue>>(false);
        internal Func<TValue> BindStaticGetter<TValue>() =>
            Bind<Func<TValue>>(true);
        private TDelegate Bind<TDelegate>(bool wantStatic) where TDelegate : Delegate {
            MethodInfo getter = prop == null || !prop.CanRead ? null : prop.GetGetMethod(true);
            if(getter == null || getter.IsStatic != wantStatic) return null;
            return BindMethod<TDelegate>(getter);
        }
    }
    private const BindingFlags InstanceMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly ConcurrentDictionary<(Type Owner, string Name), MemberInfo> InstanceMemberCache = new();
    public static bool TryRead(object target, string name, out object value) {
        value = null;
        if(target == null || string.IsNullOrEmpty(name)) return false;
        MemberInfo member = InstanceMemberCache.GetOrAdd(
            (target.GetType(), name),
            static key => ResolveInstanceMember(key.Owner, key.Name));
        if(member == null) return false;
        try {
            if(member is FieldInfo field) {
                value = field.GetValue(target);
                return true;
            }
            if(member is PropertyInfo prop) {
                value = prop.GetValue(target, null);
                return true;
            }
        } catch(Exception e) { Diag.Ignore(e); }
        return false;
    }
    private static MemberInfo ResolveInstanceMember(Type owner, string name) {
        for(Type t = owner; t != null; t = t.BaseType) {
            try {
                FieldInfo field = t.GetField(name, InstanceMembers);
                if(field != null) return field;
                PropertyInfo prop = t.GetProperty(name, InstanceMembers);
                if(prop != null && prop.GetIndexParameters().Length == 0) return prop;
            } catch(Exception e) { Diag.Ignore(e); }
        }
        return null;
    }
    public static TDelegate BindMethod<TDelegate>(MethodInfo m) where TDelegate : Delegate {
        if(m == null) return null;
        try {
            return Delegate.CreateDelegate(typeof(TDelegate), m) as TDelegate;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    private static readonly ConcurrentDictionary<(Type Owner, string Name, int ArgCount), MethodInfo> MethodCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, ParameterInfo[]> ParamCache = new();
    public static MethodInfo Method(Type owner, string name, int argCount = -1) {
        if(owner == null || string.IsNullOrEmpty(name)) return null;
        return MethodCache.GetOrAdd(
            (owner, name, argCount),
            static key => ResolveMethod(key.Owner, key.Name, key.ArgCount));
    }
    private static MethodInfo ResolveMethod(Type owner, string name, int argCount) {
        try {
            MethodInfo[] all = owner.GetMethods(Any).Where(m => m.Name == name).ToArray();
            if(all.Length == 0) return null;
            if(argCount < 0) return all[0];
            return all.FirstOrDefault(m => Params(m).Length == argCount)
                ?? all.FirstOrDefault(m => Params(m).Length >= argCount
                    && Params(m).Skip(argCount).All(p => p.IsOptional))
                ?? all[0];
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    private static ParameterInfo[] Params(MethodInfo m) =>
        ParamCache.GetOrAdd(m, static target => target.GetParameters());
    public static object Invoke(MethodInfo m, object instance, params object[] args) {
        if(m == null) return null;
        try {
            ParameterInfo[] ps = Params(m);
            if(args.Length < ps.Length) {
                object[] padded = new object[ps.Length];
                Array.Copy(args, padded, args.Length);
                for(int i = args.Length; i < ps.Length; i++)
                    padded[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                args = padded;
            }
            return m.Invoke(instance, args);
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
}
