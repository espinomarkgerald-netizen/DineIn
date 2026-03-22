using System.Collections.Generic;
using UnityEngine;

public class CustomerOrder : MonoBehaviour
{
    [System.Serializable]
    public class OrderLine
    {
        public string itemName;
        public int quantity;
        public int price;
        public List<string> contents = new List<string>();
    }

    [System.Serializable]
    public class OrderTicket
    {
        public List<OrderLine> lines = new List<OrderLine>();

        public int GetTotalPrice()
        {
            int total = 0;
            for (int i = 0; i < lines.Count; i++)
                total += lines[i].price * lines[i].quantity;
            return total;
        }
    }

    public OrderTicket currentTicket = new OrderTicket();
}