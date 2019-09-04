﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.Azure.Documents;

namespace Microsoft.Azure.Cosmos.Tests
{
    internal class ToDoItem
    {
        public string id { get; set; }
        public string pk { get; set; }
        public double cost { get; set; }
        public bool isDone { get; set; }
        public int count { get; set; }
        public string _rid { get; set; }

        public static ToDoItem Create(string idPrefix, ResourceId itemRid = null)
        {
            if(idPrefix == null)
            {
                idPrefix = string.Empty;
            }

            string id = idPrefix + Guid.NewGuid().ToString();

            return new ToDoItem()
            {
                id = id,
                pk = Guid.NewGuid().ToString(),
                cost = 9000.00001,
                isDone = true,
                count = 42,
                _rid = itemRid?.ToString()
            };
        }

        public static IList<ToDoItem> CreateItems(
            int count, 
            string idPrefix, 
            string containerRid = null)
        {
            List<ToDoItem> items = new List<ToDoItem>();
            for (uint i = 0; i < count; i++)
            {
                ResourceId rid = null;
                if(containerRid != null)
                {
                    // id 0 returns null for resource id
                    rid = ResourceId.NewDocumentId(containerRid, i+1);
                }
                
                items.Add(ToDoItem.Create(idPrefix, rid));
            }

            return items;
        }
    }

    public class ToDoItemComparer : IComparer
    {
        public int Compare(object x, object y)
        {
            if (x == y)
            {
                return 0;
            }

            ToDoItem a = x as ToDoItem;
            ToDoItem b = y as ToDoItem;

            if(a == null || b == null)
            {
                throw new ArgumentException("Invalid type");
            }

            if(a.isDone != b.isDone
                || a.count != b.count
                || a.cost != b.cost
                || !string.Equals(a.id, b.id)
                || !string.Equals(a.pk, b.pk))
            {
                return 1;
            }

            return 0;
        }
    }
}
