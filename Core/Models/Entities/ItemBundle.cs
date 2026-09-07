using Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Enums
{
    public class ItemBundle
    {
        public string? Id { get; set; }
        public int Count { get; set; }
        public ItemType Type { get; set; }
    }
}
