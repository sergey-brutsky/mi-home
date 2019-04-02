using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;

namespace MiHomeLib.Devices
{
    public abstract class MiHomeDevice<T> : IMiHomeDevice<T> where T: MiHomeDevice<T>
    {
        protected BehaviorSubject<T> _changes;

        public IObservable<IMiHomeDevice> Changes => _changes.AsObservable();

        public string Sid { get; }
        public string Name { get; set; }
        public abstract string Type { get; }

        protected MiHomeDevice(string sid)
        {
            Sid = sid;
            _changes = new BehaviorSubject<T>((T)this);
        }

        public abstract void ParseData(string command);

        protected bool ChangeAndDetectChanges<TVal>(Expression<Func<TVal>> oldExpr, TVal newValue)
        {
            var expr = (MemberExpression)oldExpr.Body;
            var prop = (PropertyInfo)expr.Member;
            var oldValue = (TVal)prop.GetValue(this);
            if (EqualityComparer<TVal>.Default.Equals(oldValue, newValue)) return false;
            prop.SetValue(this, newValue, null);
            return true;
        }
    }
}