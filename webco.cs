using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace webco
{
    public class pendingoutrequest
    {
        public AGAJMBPABDL target { get; set; } = null!;
        public AGAJMBPABDL? thrower { get; set; }
        public Vector3 position { get; set; }
        public float requesttimems { get; set; }
        public bool iscancelled { get; set; }
    }

    public class recentcatchrecord
    {
        public AGAJMBPABDL catcher { get; set; } = null!;
        public AGAJMBPABDL? thrower { get; set; }
        public float catchtimems { get; set; }
    }

    [HarmonyPatch(typeof(DodgeballManager))]
    public static class catchoutfixpatch
    {
        public static float graceperiodms = 175f;
        public static float catchhistoryms = 350f;

        private static readonly List<pendingoutrequest> _pendingouts = new List<pendingoutrequest>();
        private static readonly List<recentcatchrecord> _recentcatches = new List<recentcatchrecord>();
        private static bool _isreplayingout = false;

        public static DodgeballManager? activeinstance { get; private set; }

        private static int getplayerid(AGAJMBPABDL? p)
        {
            if (p == null) return 0;
            try
            {
                if (p.INPOAAAOAHI != 0) return p.INPOAAAOAHI;
            }
            catch { }
            return p.Pointer.ToInt32();
        }

        private static bool areplayersequal(AGAJMBPABDL? p1, AGAJMBPABDL? p2)
        {
            if (p1 == null || p2 == null) return false;
            if (p1.Pointer == p2.Pointer) return true;
            int id1 = getplayerid(p1);
            int id2 = getplayerid(p2);
            return id1 != 0 && id1 == id2;
        }

        [HarmonyPatch(nameof(DodgeballManager.Update))]
        [HarmonyPostfix]
        public static void Postfix_Update(DodgeballManager __instance)
        {
            if (__instance == null) return;
            activeinstance = __instance;

            float nowms = Time.realtimeSinceStartup * 1000f;

            for (int i = _recentcatches.Count - 1; i >= 0; i--)
            {
                if (nowms - _recentcatches[i].catchtimems > catchhistoryms)
                {
                    _recentcatches.RemoveAt(i);
                }
            }

            if (_pendingouts.Count == 0) return;

            for (int i = _pendingouts.Count - 1; i >= 0; i--)
            {
                var req = _pendingouts[i];

                if (req.iscancelled || req.target == null || req.thrower == null)
                {
                    _pendingouts.RemoveAt(i);
                    continue;
                }

                if (nowms - req.requesttimems > 2500f)
                {
                    _pendingouts.RemoveAt(i);
                    continue;
                }

                if (nowms - req.requesttimems >= graceperiodms)
                {
                    _pendingouts.RemoveAt(i);
                    try
                    {
                        _isreplayingout = true;
                        __instance.RpcMasterRequestPlayerOut(req.thrower, req.target, req.position);
                    }
                    catch { }
                    finally
                    {
                        _isreplayingout = false;
                    }
                }
            }
        }

        [HarmonyPatch(nameof(DodgeballManager.RpcMasterRequestPlayerOut))]
        [HarmonyPrefix]
        public static bool Prefix_RpcMasterRequestPlayerOut(DodgeballManager __instance, AGAJMBPABDL JPIMHOLIGNH, AGAJMBPABDL BLHKNHLBCAC, Vector3 BKNANCCLHBC)
        {
            if (_isreplayingout) return true;

            AGAJMBPABDL thrower = JPIMHOLIGNH;
            AGAJMBPABDL target = BLHKNHLBCAC;

            if (thrower == null || target == null) return true;

            float nowms = Time.realtimeSinceStartup * 1000f;

            for (int i = 0; i < _recentcatches.Count; i++)
            {
                var c = _recentcatches[i];
                if (areplayersequal(c.catcher, target) && (nowms - c.catchtimems <= catchhistoryms))
                {
                    bool ismatch = false;
                    if (c.thrower != null && thrower != null)
                    {
                        ismatch = areplayersequal(c.thrower, thrower);
                    }
                    else
                    {
                        ismatch = (nowms - c.catchtimems <= 100f);
                    }

                    if (ismatch)
                    {
                        _recentcatches.RemoveAt(i);
                        return false;
                    }
                }
            }

            _pendingouts.Add(new pendingoutrequest
            {
                target = target,
                thrower = thrower,
                position = BKNANCCLHBC,
                requesttimems = nowms,
                iscancelled = false
            });

            return false;
        }

        [HarmonyPatch(nameof(DodgeballManager.RpcMasterRequestPlayerCatch))]
        [HarmonyPrefix]
        public static bool Prefix_RpcMasterRequestPlayerCatch(DodgeballManager __instance, AGAJMBPABDL EOAKLPAPEGL, AGAJMBPABDL JPIMHOLIGNH, Vector3 ENLMKMBDDBF)
        {
            return processcatch(__instance, EOAKLPAPEGL, JPIMHOLIGNH);
        }

        [HarmonyPatch(nameof(DodgeballManager.RpcOnPlayerCatch))]
        [HarmonyPrefix]
        public static bool Prefix_RpcOnPlayerCatch(DodgeballManager __instance, AGAJMBPABDL EOAKLPAPEGL, AGAJMBPABDL JPIMHOLIGNH, Vector3 ENLMKMBDDBF)
        {
            return processcatch(__instance, EOAKLPAPEGL, JPIMHOLIGNH);
        }

        private static bool processcatch(DodgeballManager? manager, AGAJMBPABDL catcher, AGAJMBPABDL thrower)
        {
            if (catcher == null) return true;
            try
            {
                float nowms = Time.realtimeSinceStartup * 1000f;

                pendingoutrequest? unmatchedreq = null;
                int unmatchedidx = -1;

                for (int i = 0; i < _pendingouts.Count; i++)
                {
                    var req = _pendingouts[i];
                    if (!req.iscancelled && req.target != null && areplayersequal(req.target, catcher))
                    {
                        if (req.thrower != null && (thrower == null || !areplayersequal(req.thrower, thrower)))
                        {
                            unmatchedreq = req;
                            unmatchedidx = i;
                            break;
                        }
                    }
                }

                if (unmatchedreq != null && unmatchedidx != -1)
                {
                    unmatchedreq.iscancelled = true;
                    _pendingouts.RemoveAt(unmatchedidx);

                    if (manager != null && unmatchedreq.thrower != null && unmatchedreq.target != null)
                    {
                        try
                        {
                            _isreplayingout = true;
                            manager.RpcMasterRequestPlayerOut(unmatchedreq.thrower, unmatchedreq.target, unmatchedreq.position);
                        }
                        catch { }
                        finally
                        {
                            _isreplayingout = false;
                        }
                    }
                    return false;
                }

                recordcatch(catcher, thrower);
            }
            catch { }
            return true;
        }

        private static void recordcatch(AGAJMBPABDL catcher, AGAJMBPABDL? thrower)
        {
            if (catcher == null) return;
            try
            {
                float nowms = Time.realtimeSinceStartup * 1000f;

                for (int i = 0; i < _recentcatches.Count; i++)
                {
                    var r = _recentcatches[i];
                    if (areplayersequal(r.catcher, catcher) && (r.thrower == null || thrower == null || areplayersequal(r.thrower, thrower)) && (nowms - r.catchtimems < 200f))
                    {
                        return;
                    }
                }

                _recentcatches.Add(new recentcatchrecord
                {
                    catcher = catcher,
                    thrower = thrower,
                    catchtimems = nowms
                });

                for (int i = 0; i < _pendingouts.Count; i++)
                {
                    var req = _pendingouts[i];
                    if (!req.iscancelled && req.target != null && areplayersequal(req.target, catcher))
                    {
                        if (thrower != null && req.thrower != null && areplayersequal(req.thrower, thrower))
                        {
                            req.iscancelled = true;
                            break;
                        }
                        else if (thrower == null && req.thrower == null)
                        {
                            req.iscancelled = true;
                            break;
                        }
                    }
                }
            }
            catch { }
        }
    }
}
