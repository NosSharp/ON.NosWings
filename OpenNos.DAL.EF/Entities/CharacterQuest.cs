﻿namespace OpenNos.DAL.EF
{
    public class CharacterQuest : SynchronizableBaseEntity
    {
        #region Properties

        public virtual Character Character { get; set; }

        public long CharacterId { get; set; }

        public virtual Quest Quest { get; set; }

        public long QuestId { get; set; }

        public int? FirstObjective { get; set; }

        public int? SecondObjective { get; set; }

        public int? ThirdObjective { get; set; }

        #endregion
    }
}