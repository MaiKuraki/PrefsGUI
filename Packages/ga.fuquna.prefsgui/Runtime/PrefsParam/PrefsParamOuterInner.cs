﻿using System;
using System.Collections.Generic;
using PrefsGUI.Kvs;
using UnityEngine;
using UnityEngine.Assertions;

namespace PrefsGUI
{
    /// <summary>
    /// Basic implementation of TOuter and TInner
    /// デフォルト値は各インスタンスごとに固有で持つが、Get(),Set()はKvsと透過的に行う（＝同一キーなら同一の値）
    /// </summary>
    public abstract class PrefsParamOuterInner<TOuter, TInner> : PrefsParamOuter<TOuter>
    {
        #region Type Define
        
        public class CachedValue<T>
        {
            private T _value;

            public bool HasValue { get; private set; }

            public bool TryGet(out T v)
            {
                v = _value;
                return HasValue;
            }

            public void Set(T v)
            {
                _value = v;
                HasValue = true;
            }

            public void Clear() => HasValue = false;
        }
        
        public class OuterInnerCache
        {
            public readonly CachedValue<TOuter> outer = new();
            public readonly CachedValue<TInner> inner = new();

            public void Clear()
            {
                outer.Clear();
                inner.Clear();
            }
        }
        
        #endregion
        

        private static readonly Dictionary<string, OuterInnerCache> KeyToCache = new();
        

        private CachedValue<TInner> _defaultValueInnerCache = new();
        private OuterInnerCache _cache;
        private PrefsInnerAccessor _prefsInnerAccessor;
        
        protected PrefsParamOuterInner(string key, TOuter defaultValue = default) : base(key, defaultValue)
        {
        }

        protected virtual bool Equals(TInner lhs, TInner rhs) => EqualityComparer<TInner>.Default.Equals(lhs, rhs);
        
        protected TInner GetDefaultInner()
        {
            if (!_defaultValueInnerCache.TryGet(out var value))
            {
                value = ToInner(defaultValue);
                _defaultValueInnerCache.Set(value);
            }
            
            return value;
        }

        private TInner GetInner()
        {
            if (!_cache.inner.TryGet(out var value))
            {
                value = PrefsKvs.Get(key, GetDefaultInner());
                _cache.inner.Set(value);
            }

            return value;
        }
        
        protected bool SetInner(TInner v)
        {
            var updateValue = !Equals(v, GetInner());
            if (updateValue)
            {
                PrefsKvs.Set(key, v);
                if (typeof(TInner).IsValueType)
                {
                    _cache.Clear();
                }
                // 非ValueTypeならできるだけ参照先を保持したいため、_cache.outer.Clear()はInnerと異なる場合のみ行う
                // 参照先が異なると以前と同じ値がどうか判定が難しい → つねに変更有りと過程して不必要なUIの更新が発生するケースがある
                // UIが更新されるとドラッグなどUI上の操作の継続が難しくなる
                // のでキャッシュのクリアはより上位の処理で選択できるようにここでは行わない
                else 
                {
                    _cache.inner.Clear();
                    var skipClearOuter = _cache.outer.TryGet(out var outerValue) && Equals(ToInner(outerValue), GetInner());
                    if (!skipClearOuter)
                    {
                        _cache.outer.Clear();
                    }
                }

                OnValueChanged();
            }

            return updateValue;
        }


        #region abstract

        protected abstract TOuter ToOuter(TInner inner);
        protected abstract TInner ToInner(TOuter outer);

        #endregion


        #region override

        public override void Reset()
        {
            base.Reset();
            _defaultValueInnerCache.Clear();
            _prefsInnerAccessor = null;
        }

        public override void ClearCache()
        {
            base.ClearCache();
            _cache.Clear();
        }
        
        protected override void OnKeyChanged(string oldKey, string newKey)
        {
            if (!KeyToCache.TryGetValue(newKey, out _cache))
            {
                KeyToCache[newKey] = _cache = new ();
            }
            
            base.OnKeyChanged(oldKey, newKey);
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            
            // defaultValueがInspectorで書き換えられてる可能性がある
            _defaultValueInnerCache ??= new();
            _defaultValueInnerCache.Clear();
        }

        public override TOuter Get()
        {
            if (!_cache.outer.TryGet(out var value))
            {
                value = ToOuter(GetInner());
                _cache.outer.Set(value);
            }

            return value;
        }

        public override bool Set(TOuter v) => SetInner(ToInner(v));

        public override Type GetInnerType() => typeof(TInner);

        public override bool IsDefault => Equals(GetDefaultInner(), GetInner());

        public override void SetCurrentToDefault()
        {
            // TOuterがクラスだと、defaultValueと今後Get()で返ってくるインスタンスが同一なってしまい、
            // 外部でdefaultValueの値を書き換えることが可能になってしまう。したがって次のコードはまずい
            //
            // 　defaultValue = Get();
            //
            // ToOuter(GetInner())で新しいインスタンスを作る
            // TInnerがクラスでToOuter()で何もしない処理だと同様の問題があるが、現状TInnerがクラスなのはstringのみなので大丈夫
            defaultValue = ToOuter(GetInner());
            _defaultValueInnerCache.Clear();
        }

        public override IPrefsInnerAccessor<T> GetInnerAccessor<T>()
        {
            Assert.AreEqual(typeof(T), typeof(TInner));
            _prefsInnerAccessor ??= new(this);
            return (IPrefsInnerAccessor<T>) _prefsInnerAccessor;
        }

        #endregion


        #region InnerAccessor

        public class PrefsInnerAccessor : IPrefsInnerAccessor<TInner>
        {
            private readonly PrefsParamOuterInner<TOuter, TInner> _prefs;
            private bool _hasSyncedValue;
            private TInner _syncedValue;

            public PrefsInnerAccessor(PrefsParamOuterInner<TOuter, TInner> prefs)
            {
                _prefs = prefs;
                _prefs.RegisterValueChangedCallback(OnValueChanged);
            }

            private void OnValueChanged()
            {
                if (!_hasSyncedValue) return;
                _prefs.Synced = Equals(_syncedValue, Get());
            }

            #region IPrefsInnerAccessor

            public PrefsParam Prefs => _prefs;

            public bool IsAlreadyGet => _prefs._cache.outer.HasValue || _prefs._cache.inner.HasValue;
            public TInner Get() => _prefs.GetInner();

            public bool SetSyncedValue(TInner value)
            {
                var ret =  _prefs.SetInner(value);
                _prefs.Synced = true;
                _syncedValue = value;
                _hasSyncedValue = true;
                
                return ret;
            }

            public bool Equals(TInner lhs, TInner rhs) => _prefs.Equals(lhs, rhs);
            
            #endregion
        }

        #endregion
    }
}