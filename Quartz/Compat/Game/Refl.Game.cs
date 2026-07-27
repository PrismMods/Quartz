using System;
using Quartz.Core;
namespace Quartz.Compat.Game;
public static partial class Refl {
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
