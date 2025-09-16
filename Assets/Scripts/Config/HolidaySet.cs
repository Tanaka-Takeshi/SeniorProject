using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Config
{
    [CreateAssetMenu(menuName = "Game/Config/HolidaySet")]
    public class HolidaySet : ScriptableObject
    {
        // "YYYY-MM-DD"
        public List<string> isoDates = new();

        public bool IsHoliday(DateTime gameDate)
        {
            // 月はMM（大文字）。mmは「分」なので注意。
            return isoDates.Contains(gameDate.ToString("yyyy-MM-dd"));
        }
    }
}
