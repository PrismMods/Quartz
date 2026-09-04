using System;
using HarmonyLib;
using Quartz.Core;
namespace Quartz.Compat.Game;
public static partial class Refl {
    internal sealed partial class Member {
        internal Func<TOwner, TValue> BindFieldGetter<TOwner, TValue>() where TOwner : class {
            if(fieldInfo == null || fieldInfo.IsStatic) return null;
            try {
                AccessTools.FieldRef<TOwner, TValue> field = AccessTools.FieldRefAccess<TOwner, TValue>(fieldInfo);
                return field == null ? null : o => o == null ? default : field(o);
            } catch(Exception e) {
                Diag.Ignore(e);
                return null;
            }
        }
        internal Func<TOwner, TValue> BindAnyGetter<TOwner, TValue>() where TOwner : class =>
            BindGetter<TOwner, TValue>() ?? BindFieldGetter<TOwner, TValue>();
    }
    public static Type Type(string name) {
        if(string.IsNullOrEmpty(name)) return null;
        try {
            return typeof(ADOBase).Assembly.GetType(name)
                ?? typeof(ADOBase).Assembly.GetType("ADOFAI." + name);
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
}
