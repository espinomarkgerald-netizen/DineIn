using UnityEngine;

public class TutorialCashierOrderRandomizer : MonoBehaviour
{
    public struct GeneratedOrder
    {
        public int orderNumber;

        public string firstFoodName;
        public Sprite firstFoodSprite;

        public string secondFoodName;
        public Sprite secondFoodSprite;

        public string drinkName;
        public Sprite drinkSprite;

        public int foodTotal;
        public int drinkTotal;
        public int total;
        public int received;
    }

    [Header("Food Sprites")]
    [SerializeField] private Sprite chickenSprite;
    [SerializeField] private Sprite friesSprite;
    [SerializeField] private Sprite burgerSprite;

    [Header("Drink Sprites")]
    [SerializeField] private Sprite cokeSprite;
    [SerializeField] private Sprite pineappleSprite;
    [SerializeField] private Sprite icedTeaSprite;

    [Header("Order Numbers")]
    [SerializeField] private int minOrderNumber = 3001;
    [SerializeField] private int maxOrderNumber = 3999;

    [Header("Possible Received Amounts")]
    [SerializeField] private int[] receivedCandidates = { 100, 200, 500, 1000 };

    public GeneratedOrder Generate()
    {
        GeneratedOrder order = new GeneratedOrder();

        order.orderNumber = Random.Range(minOrderNumber, maxOrderNumber + 1);

        int foodPattern = Random.Range(0, 6);
        switch (foodPattern)
        {
            case 0:
                order.firstFoodName = "Chicken";
                order.firstFoodSprite = chickenSprite;
                order.secondFoodName = null;
                order.secondFoodSprite = null;
                order.foodTotal = 299;
                break;

            case 1:
                order.firstFoodName = "Fries";
                order.firstFoodSprite = friesSprite;
                order.secondFoodName = null;
                order.secondFoodSprite = null;
                order.foodTotal = 79;
                break;

            case 2:
                order.firstFoodName = "Burger";
                order.firstFoodSprite = burgerSprite;
                order.secondFoodName = null;
                order.secondFoodSprite = null;
                order.foodTotal = 119;
                break;

            case 3:
                order.firstFoodName = "Chicken";
                order.firstFoodSprite = chickenSprite;
                order.secondFoodName = "Fries";
                order.secondFoodSprite = friesSprite;
                order.foodTotal = 349;
                break;

            case 4:
                order.firstFoodName = "Chicken";
                order.firstFoodSprite = chickenSprite;
                order.secondFoodName = "Burger";
                order.secondFoodSprite = burgerSprite;
                order.foodTotal = 399;
                break;

            default:
                order.firstFoodName = "Burger";
                order.firstFoodSprite = burgerSprite;
                order.secondFoodName = "Fries";
                order.secondFoodSprite = friesSprite;
                order.foodTotal = 179;
                break;
        }

        int drinkPattern = Random.Range(0, 3);
        switch (drinkPattern)
        {
            case 0:
                order.drinkName = "Coke";
                order.drinkSprite = cokeSprite;
                break;

            case 1:
                order.drinkName = "Pineapple";
                order.drinkSprite = pineappleSprite;
                break;

            default:
                order.drinkName = "Ice Tea";
                order.drinkSprite = icedTeaSprite;
                break;
        }

        order.drinkTotal = 50;
        order.total = order.foodTotal + order.drinkTotal;
        order.received = GetRandomReceivedAmountAbove(order.total);

        return order;
    }

    private int GetRandomReceivedAmountAbove(int total)
    {
        if (receivedCandidates != null && receivedCandidates.Length > 0)
        {
            int[] valid = new int[receivedCandidates.Length];
            int count = 0;

            for (int i = 0; i < receivedCandidates.Length; i++)
            {
                if (receivedCandidates[i] > total)
                {
                    valid[count] = receivedCandidates[i];
                    count++;
                }
            }

            if (count > 0)
            {
                int index = Random.Range(0, count);
                return valid[index];
            }
        }

        int rounded = ((total / 100) + 1) * 100;
        return Mathf.Max(rounded, total + 1);
    }
}