using KKAPI;
using KKAPI.Chara;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KK_Things
{
    internal class DevFiddleCharaController : CharaCustomFunctionController
    {
        private static readonly DynamicBoneSetting[] _dbSettings =
        [
            new (
                name: "BodyTop/p_cf_body_bone/cf_j_root/cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_d_kokan/cm_J_dan_top/cm_J_dan100_00/cm_J_dan101_00/cm_J_dan102_00",
                state: false
                ),

            new (
                name: "BodyTop/p_cf_body_bone/cf_j_root/cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_d_kokan/cm_J_dan_top/cm_J_dan_f_top/cm_J_dan_f_L/cm_J_dan_Pivot_f_L",
                state: true
                ),

            new (
                name: "BodyTop/p_cf_body_bone/cf_j_root/cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_d_kokan/cm_J_dan_top/cm_J_dan_f_top/cm_J_dan_f_R/cm_J_dan_Pivot_f_R",
                state: true
                ),
        ];

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {

        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            OnLoadAnimation();
        }

        internal void OnLoadAnimation()
        {
            var chara = ChaControl;

            if (chara.sex != 0) return;

            foreach (var db in _dbSettings)
            {
                UpdateDB(chara.transform.Find(db.name), db.state);
            }



            static void UpdateDB(Transform bone, bool state = true)
            {
                if (bone == null) return;

                var db = bone.GetComponent<DynamicBone>();

                if (db == null) return;

                db.enabled = state;
                db.m_Force = Vector3.zero;
            }
        }

        private readonly struct DynamicBoneSetting(string name, bool state)
        {
            internal readonly string name = name;
            internal readonly bool state = state;
        }

    }
}
