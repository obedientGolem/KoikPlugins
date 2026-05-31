using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LBUtils
{
    public class LBUtilsMath
    {
#if KK
        public static Quaternion Normalize(Quaternion q)
        {
            float num = Mathf.Sqrt(Quaternion.Dot(q, q));
            bool flag = num < Mathf.Epsilon;
            Quaternion result;
            if (flag)
            {
                result = Quaternion.identity;
            }
            else
            {
                num = 1f / num;
                result = new Quaternion(q.x * num, q.y * num, q.z * num, q.w * num);
            }
            return result;
        }
#endif
    }
}
