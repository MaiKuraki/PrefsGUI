﻿using RosettaUI;

namespace PrefsGUI.RosettaUI
{
    public static class PrefsGUIExtensionSlider
    {
        public static Element CreateSlider<T>(this PrefsParamOuter<T> prefs, LabelElement label = null)
        {
            IGetter<T> minGetter = null;
            IGetter<T> maxGetter = null;
            if (prefs is IPrefsSlider<T> prefsSlider)
            {
                minGetter = ConstGetter.Create(prefsSlider.defaultMin);
                maxGetter = ConstGetter.Create(prefsSlider.defaultMax);
            }

            return CreateSlider(prefs, label, minGetter, maxGetter);
        }

        public static Element CreateSlider<T>(this PrefsParamOuter<T> prefs, T max)
        {
            return CreateSlider<T>(prefs, null, default, max);
        }
        
        public static Element CreateSlider<T>(this PrefsParamOuter<T> prefs, T min, T max)
        {
            return CreateSlider(prefs, null, min, max);
        }
        
        public static Element CreateSlider<T>(this PrefsParamOuter<T> prefs, LabelElement label, T max)
        {
            return CreateSlider(prefs, label, default, max);
        }

        public static Element CreateSlider<T>(this PrefsParamOuter<T> prefs, LabelElement label, T min, T max)
        {
            return CreateSlider(prefs, label, ConstGetter.Create(min), ConstGetter.Create(max));
        }

        public static Element CreateSlider<T>(this PrefsParamOuter<T> prefs, LabelElement label, IGetter<T> minGetter, IGetter<T> maxGetter)
        {
            var element = UI.Row(
                CreateSliderRaw(prefs, label ?? UI.Label(() => prefs.key), minGetter, maxGetter),
                prefs.CreateDefaultButtonElement()
            );
            
            PrefsGUIExtension.SubscribeSyncedFlag(prefs, element);

            return element;
        }

        public static Element CreateSliderRaw<T>(this PrefsParamOuter<T> prefs, LabelElement label, T max)
        {
            return CreateSliderRaw(prefs, label, default, max);
        }
        
        public static Element CreateSliderRaw<T>(this PrefsParamOuter<T> prefs, LabelElement label, T min, T max)
        {
            return CreateSliderRaw(prefs, label, ConstGetter.Create(min), ConstGetter.Create(max));
        }
        
        public static Element CreateSliderRaw<T>(this PrefsParamOuter<T> prefs, LabelElement label, IGetter<T> minGetter, IGetter<T> maxGetter)
        {
            return UI.Slider(
                label,
                Binder.Create(prefs.Get, v => prefs.Set(v)),
                minGetter,
                maxGetter
            );
        }
    }
}