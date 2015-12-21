//------------------------------------------------------------------------------
// <auto-generated>
//     Ce code a été généré à partir d'un modèle.
//
//     Des modifications manuelles apportées à ce fichier peuvent conduire à un comportement inattendu de votre application.
//     Les modifications manuelles apportées à ce fichier sont remplacées si le code est régénéré.
// </auto-generated>
//------------------------------------------------------------------------------

namespace OpenNos.DAL.EF.MySQL.DB
{
    using System;
    using System.Collections.Generic;
    
    public partial class Character
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Character()
        {
            this.generallog = new HashSet<GeneralLog>();
            this.inventory = new HashSet<Inventory>();
        }
    
        public long CharacterId { get; set; }
        public long AccountId { get; set; }
        public short MapId { get; set; }
        public string Name { get; set; }
        public byte Slot { get; set; }
        public byte Gender { get; set; }
        public byte Class { get; set; }
        public byte HairStyle { get; set; }
        public byte HairColor { get; set; }
        public short MapX { get; set; }
        public short MapY { get; set; }
        public int Hp { get; set; }
        public int Mp { get; set; }
        public int ArenaWinner { get; set; }
        public int Reput { get; set; }
        public int Dignite { get; set; }
        public long Gold { get; set; }
        public int Backpack { get; set; }
        public byte Level { get; set; }
        public int LevelXp { get; set; }
        public byte JobLevel { get; set; }
        public int JobLevelXp { get; set; }
        public int Dead { get; set; }
        public int Kill { get; set; }
        public int Contribution { get; set; }
        public int Faction { get; set; }
        public byte State { get; set; }
    
        public virtual Account account { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<GeneralLog> generallog { get; set; }
        public virtual Map map { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Inventory> inventory { get; set; }
    }
}
