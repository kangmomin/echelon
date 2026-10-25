using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Presentation.Models
{
    public enum EquipmentMeshRole
    {
        Camo = 0,
        Dark = 1,
        Marker = 2
    }

    public enum EquipmentModelKind
    {
        Unknown = 0,
        Abrams,
        AbramsTusk,
        T90,
        T72,
        T80,
        Bradley,
        BradleyFist,
        Bmp3,
        Brm3K,
        Bmp2,
        Bmp1Recon,
        Bmp1Command,
        Nona,
        Tor,
        HumveeAvenger,
        HumveeJtac,
        Truck,
        TruckFo,
        Hemtt,
        Jammer,
        M113Apc,
        M113Cp,
        M113Mortar,
        InfantrySquad,
        InfantrySniper,
        InfantryAt,
        InfantryHq,
        StrykerIcv,
        StrykerDragoon,
        StrykerAtgm,
        StrykerMshorad,
        Btr82A,
        HumveeTow,
        Booker,
        Abv,
        Bmpt,
        ShturmS,
        Ur77,
        Tunguska,
        Apache,
        Ka52
    }

    public sealed class EquipmentModelHandle
    {
        public string ElementId { get; internal set; }
        public string ElementClass { get; internal set; }
        public string Side { get; internal set; }
        public EquipmentModelKind Kind { get; internal set; }

        public GameObject Root { get; internal set; }
        public Transform RootTransform { get { return Root.transform; } }
        public Transform Tilt { get; internal set; }
        public Transform Hull { get; internal set; }
        public Transform Turret { get; internal set; }
        public Transform Rotor { get; internal set; }
        public Transform Rotor2 { get; internal set; }

        public Mesh HullMesh { get; internal set; }
        public Mesh TurretMesh { get; internal set; }
        public Mesh RotorMesh { get; internal set; }
        public Mesh Rotor2Mesh { get; internal set; }

        public MeshRenderer HullRenderer { get; internal set; }
        public MeshRenderer TurretRenderer { get; internal set; }
        public MeshRenderer RotorRenderer { get; internal set; }
        public MeshRenderer Rotor2Renderer { get; internal set; }

        public IReadOnlyList<EquipmentMeshRole> HullRoles { get; internal set; }
        public IReadOnlyList<EquipmentMeshRole> TurretRoles { get; internal set; }
        public IReadOnlyList<EquipmentMeshRole> RotorRoles { get; internal set; }
        public IReadOnlyList<EquipmentMeshRole> Rotor2Roles { get; internal set; }

        public bool HasTurret { get { return Turret != null; } }
        public int RotorCount
        {
            get
            {
                if (Rotor2 != null)
                {
                    return 2;
                }
                return Rotor != null ? 1 : 0;
            }
        }

        internal EquipmentModelHandle()
        {
        }
    }
}
