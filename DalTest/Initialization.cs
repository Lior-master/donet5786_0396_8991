namespace DalTest;

using Dal;
using DalApi;
using DO;
using System.Diagnostics.Metrics;

/// <summary>
/// Represents the initialization component in this layer.
/// </summary>
public static class Initialization
{
   
    /// <summary>
    /// Stores the s dal value.
    /// </summary>
    private static IDal? s_dal;

    private static readonly Random s_rand = new();
    private const int MIN_ID = 200000000;
    private const int MAX_ID = 400000000;


    /// <summary>
    /// Represents the adresses component in this layer.
    /// </summary>
    public class Adresses
    {
        /// <summary>
        /// Gets or sets the street value.
        /// </summary>
        public string Street { get; set; }
        /// <summary>
        /// Gets or sets the latitude value.
        /// </summary>
        public double Latitude { get; set; }
        /// <summary>
        /// Gets or sets the longitude value.
        /// </summary>
        public double Longitude { get; set; }
        /// <summary>
        /// Gets or sets the distance from company value.
        /// </summary>
        public double DistanceFromCompany { get; set; }
        /// <summary>
        /// Gets or sets the distance walking from company value.
        /// </summary>
        public double DistanceWalkingFromCompany { get; set; }
        /// <summary>
        /// Gets or sets the distance car from company value.
        /// </summary>
        public double DistanceCarFromCompany { get; set; } 

        /// <summary>
        /// Initializes a new instance of the Adresses class.
        /// </summary>
        /// <param name="street">The street value.</param>
        /// <param name="latitude">The latitude value.</param>
        /// <param name="longitude">The longitude value.</param>
        /// <param name="distanceFromCompany">The distance from company value.</param>
        /// <param name="distanceWalkingFromCompany">The distance walking from company value.</param>
        /// <param name="distanceCarFromCompany">The distance car from company value.</param>
        public Adresses(string street, double latitude, double longitude, double distanceFromCompany, double distanceWalkingFromCompany, double distanceCarFromCompany)
        {
            Street = street;
            Latitude = latitude;
            Longitude = longitude;
            DistanceFromCompany = distanceFromCompany;
            DistanceWalkingFromCompany = distanceWalkingFromCompany;
            DistanceCarFromCompany = distanceCarFromCompany;
        }
        // to calculate the distance between two coordinates
    }

    private static void createCouriers()
    {
        s_dal!.Courier.Create(new Courier(
            Id: 347657991,
            Name: "Boss",
            Phone: "+111111111",
            Email: "boss@company.com",
            Password: "admin",
            IsActive: true,
            Transport: DeliveryTransport.Car,
            StartDate: DateTime.Now,
            MaxDistance: 999,
            Administrator: Administrator.Director
        ));

        for (int i = 0; i < 40; i++)
        {
            var transport = (DeliveryTransport)s_rand.Next(0, 4);

            double maxDistance = transport switch
            {
                DeliveryTransport.Foot => s_rand.Next(0, 3),   // 0–2 km
                DeliveryTransport.Bike => s_rand.Next(3, 8),   // 3–7 km
                DeliveryTransport.Motorcycle => s_rand.Next(8, 15), // 8–14 km
                DeliveryTransport.Car => s_rand.Next(15, 50), // 15–49 km
                _ => 1
            };

            // 80% chance d'être actif (1-80 = actif, 81-100 = inactif)
            bool isActive = s_rand.Next(1, 101) <= 80;

            Courier courier = new Courier
            (
                Id: i+100001,
                Name: $"Courier_{i + 1}",
                Phone: $"+100000000{i + 1:D2}",
                Email: $"Courier_{i + 1}@gmail.com",
                Password: "password",
                IsActive: isActive,
                Transport: transport,
                StartDate: DateTime.Now.AddDays(s_rand.Next(-365, 0)),
                MaxDistance: maxDistance,
                Administrator : Administrator.Courier
            );

            s_dal!.Courier.Create(courier);
        }
    }

    private static void createOrders()
    {
        for (int i = 0; i < 60; i++)
        {
            OrderType Type = (OrderType)s_rand.Next(0, 5);
            

            var adress = addresses[s_rand.Next(0,5)];
            Order order = new Order
            (
                Id: 0,
                CustomerName: $"Customer_{i + 1}",
                CustomerAddress: adress.Street,
                CustomerPhone: $"+200000000{i + 1:D2}",
                Type: Type,
                // use DAL clock to keep all dates consistent with BL clock
                OrderDate: s_dal!.Config.Clock.AddMinutes(-s_rand.Next(0, 21)),
                Latitude: null, // automatic assignement based on address in BL
                Longitude: null
            );
            s_dal!.Order.Create(order);
        }
    }

    private static void createDeliveries()
    {
        // Get all existing orders and couriers
        var orders = s_dal!.Order.ReadAll().ToList();
        var allCouriers = s_dal!.Courier.ReadAll().ToList();

        // Filter: active, not Director
        var availableCouriers = allCouriers
            .Where(c => c.IsActive && c.Administrator != Administrator.Director)
            .ToList();

        if (orders.Count == 0 || availableCouriers.Count == 0)
        {
            Console.WriteLine("No orders or available couriers to create deliveries.");
            return;
        }

        // Track orders and their delivery attempts for realistic retry scenarios
        var orderDeliveryAttempts = new Dictionary<int, List<(int courierId, DO.DeliveredStatus status)>>();
        int totalDeliveriesCreated = 0;
        int maxDeliveries = Math.Min(60, orders.Count * 2); // Allow up to 2 attempts per order on average

        // First pass: Create initial deliveries for most orders
        var usedOrders = new HashSet<int>();
        int firstPassDeliveries = 0;
        
        while (firstPassDeliveries < Math.Min(45, orders.Count) && firstPassDeliveries < 1000)
        {
            var order = orders[s_rand.Next(orders.Count)];
            if (usedOrders.Contains(order.Id)) continue;

            var courier = availableCouriers[s_rand.Next(availableCouriers.Count)];
            
            DateTime pickup = order.OrderDate.AddMinutes(s_rand.Next(5, 26));
            DO.DeliveredStatus? deliveryStatus;
            DateTime? arrivalTime = null;
            
            // More realistic first-attempt distribution:
            // 60% successful, 20% in progress, 15% customer issues (can retry), 5% system failures
            int statusRoll = s_rand.Next(1, 101);
            
            if (statusRoll <= 60) // 60% - Successfully Delivered
            {
                deliveryStatus = DO.DeliveredStatus.Delivered;
                int deliveryTimeMinutes = courier.Transport switch
                {
                    DeliveryTransport.Foot => s_rand.Next(25, 45),
                    DeliveryTransport.Bike => s_rand.Next(15, 30),
                    DeliveryTransport.Motorcycle => s_rand.Next(10, 25),
                    DeliveryTransport.Car => s_rand.Next(8, 20),
                    _ => s_rand.Next(15, 30)
                };
                arrivalTime = pickup.AddMinutes(deliveryTimeMinutes);
                int delayMinutes = (int)(Math.Pow(s_rand.NextDouble(), 2) * 10);
                arrivalTime = arrivalTime.Value.AddMinutes(delayMinutes);
            }
            else if (statusRoll <= 80) // 20% - In Progress
            {
                deliveryStatus = null;
                // FIXED: For in-progress orders, pickup should be AFTER order date but BEFORE current time
                // Calculate a realistic pickup time between order placement and now
                var timeSinceOrder = s_dal.Config.Clock - order.OrderDate;
                if (timeSinceOrder.TotalMinutes > 5) // Only if order was placed more than 5 minutes ago
                {
                    // Pickup happened sometime after order was placed but before now
                    var maxPickupDelay = Math.Min((int)timeSinceOrder.TotalMinutes - 2, 60); // Leave 2 minutes buffer
                    pickup = order.OrderDate.AddMinutes(s_rand.Next(5, Math.Max(6, maxPickupDelay)));
                }
                else
                {
                    // Recent order - use normal pickup time
                    pickup = order.OrderDate.AddMinutes(s_rand.Next(5, 26));
                }
                arrivalTime = null;
            }
            else if (statusRoll <= 95) // 15% - Customer Issues (Absent, Rejected) - CAN BE RETRIED
            {
                var retryableStatuses = new[] { DO.DeliveredStatus.Absent, DO.DeliveredStatus.Rejected };
                deliveryStatus = retryableStatuses[s_rand.Next(retryableStatuses.Length)];
                
                int baseTimeMinutes = courier.Transport switch
                {
                    DeliveryTransport.Foot => s_rand.Next(25, 45),
                    DeliveryTransport.Bike => s_rand.Next(15, 30),
                    DeliveryTransport.Motorcycle => s_rand.Next(10, 25),
                    DeliveryTransport.Car => s_rand.Next(8, 20),
                    _ => s_rand.Next(15, 30)
                };
                
                int attemptMinutes = s_rand.Next(5, 16);
                arrivalTime = pickup.AddMinutes(baseTimeMinutes + attemptMinutes);
                
                // Track this as a failed attempt that can be retried
                if (!orderDeliveryAttempts.ContainsKey(order.Id))
                    orderDeliveryAttempts[order.Id] = new List<(int, DO.DeliveredStatus)>();
                orderDeliveryAttempts[order.Id].Add((courier.Id, deliveryStatus.Value));
            }
            else // 5% - System Failures (Canceled, Failed) - SOME CAN BE RETRIED
            {
                var systemStatuses = new[] { DO.DeliveredStatus.Canceled, DO.DeliveredStatus.Failed };
                deliveryStatus = systemStatuses[s_rand.Next(systemStatuses.Length)];
                
                if (s_rand.NextDouble() < 0.7) // 70% were attempted before failure
                {
                    int partialTimeMinutes = s_rand.Next(10, 30);
                    arrivalTime = pickup.AddMinutes(partialTimeMinutes);
                    
                    // Only Failed deliveries can be retried, not Canceled ones
                    if (deliveryStatus == DO.DeliveredStatus.Failed)
                    {
                        if (!orderDeliveryAttempts.ContainsKey(order.Id))
                            orderDeliveryAttempts[order.Id] = new List<(int, DO.DeliveredStatus)>();
                        orderDeliveryAttempts[order.Id].Add((courier.Id, deliveryStatus.Value));
                    }
                }
            }

            // Ensure completed deliveries have valid timestamps
            if (deliveryStatus.HasValue && pickup > s_dal.Config.Clock)
            {
                pickup = s_dal.Config.Clock.AddMinutes(-s_rand.Next(10, 120));
                if (arrivalTime.HasValue)
                {
                    var originalDuration = arrivalTime.Value.Subtract(order.OrderDate);
                    arrivalTime = pickup.Add(originalDuration);
                }
            }

            // FINAL VALIDATION: Ensure pickup is NEVER before order date
            if (pickup < order.OrderDate)
            {
                pickup = order.OrderDate.AddMinutes(s_rand.Next(5, 15));
                
                // Recalculate arrival time if needed
                if (arrivalTime.HasValue)
                {
                    var deliveryDuration = arrivalTime.Value.Subtract(pickup);
                    if (deliveryDuration.TotalMinutes < 5) // Minimum delivery time
                    {
                        arrivalTime = pickup.AddMinutes(s_rand.Next(8, 25));
                    }
                }
            }

            // Create the delivery
            Delivery delivery = new Delivery
            (
                Id: 0,
                OrderId: order.Id,
                CourierId: courier.Id,
                PickupTime: pickup,
                Transport: courier.Transport,
                ArrivalTime: arrivalTime,
                Distance: null,
                DeliveredStatus: deliveryStatus
            );

            s_dal!.Delivery.Create(delivery);
            usedOrders.Add(order.Id);
            firstPassDeliveries++;
            totalDeliveriesCreated++;
        }

        // Second pass: Create retry attempts for orders that failed/were absent
        int retryAttempts = 0;
        var ordersToRetry = orderDeliveryAttempts.Keys.ToList();
        
        while (retryAttempts < 15 && totalDeliveriesCreated < maxDeliveries && ordersToRetry.Count > 0)
        {
            var orderIdToRetry = ordersToRetry[s_rand.Next(ordersToRetry.Count)];
            var order = orders.First(o => o.Id == orderIdToRetry);
            var previousAttempts = orderDeliveryAttempts[orderIdToRetry];
            
            // 70% chance to actually retry (some customers/situations may not get retried)
            if (s_rand.NextDouble() > 0.7)
            {
                ordersToRetry.Remove(orderIdToRetry);
                continue;
            }
            
            // Choose a different courier for retry (if possible)
            var previousCourierIds = previousAttempts.Select(a => a.courierId).ToHashSet();
            var availableForRetry = availableCouriers.Where(c => !previousCourierIds.Contains(c.Id)).ToList();
            
            if (availableForRetry.Count == 0)
                availableForRetry = availableCouriers; // Fallback to any courier
            
            var retryCourier = availableForRetry[s_rand.Next(availableForRetry.Count)];
            
            // Retry happens some time after the first failure (30 minutes to 3 hours later)
            var lastFailure = s_dal.Delivery.ReadAll()
                .Where(d => d.OrderId == orderIdToRetry)
                .OrderByDescending(d => d.ArrivalTime ?? d.PickupTime)
                .First();
                
            DateTime retryPickup = (lastFailure.ArrivalTime ?? lastFailure.PickupTime)
                .AddMinutes(s_rand.Next(30, 180)); // 30 minutes to 3 hours later
                
            // Make sure retry is not in the future
            if (retryPickup > s_dal.Config.Clock)
            {
                retryPickup = s_dal.Config.Clock.AddMinutes(-s_rand.Next(10, 60));
            }
            
            // ENSURE retry pickup is after original order date
            if (retryPickup < order.OrderDate)
            {
                retryPickup = order.OrderDate.AddMinutes(s_rand.Next(60, 120)); // 1-2 hours after original order
                if (retryPickup > s_dal.Config.Clock)
                {
                    retryPickup = s_dal.Config.Clock.AddMinutes(-s_rand.Next(5, 30));
                }
            }
            
            DO.DeliveredStatus? retryStatus;
            DateTime? retryArrival = null;
            
            // Retry attempts have higher success rate (80% success, 20% fail again)
            if (s_rand.NextDouble() <= 0.8) // 80% - Successful retry
            {
                retryStatus = DO.DeliveredStatus.Delivered;
                int retryTimeMinutes = retryCourier.Transport switch
                {
                    DeliveryTransport.Foot => s_rand.Next(25, 45),
                    DeliveryTransport.Bike => s_rand.Next(15, 30),
                    DeliveryTransport.Motorcycle => s_rand.Next(10, 25),
                    DeliveryTransport.Car => s_rand.Next(8, 20),
                    _ => s_rand.Next(15, 30)
                };
                retryArrival = retryPickup.AddMinutes(retryTimeMinutes);
            }
            else // 20% - Still fails (but different reason possibly)
            {
                var retryFailureStatuses = new[] { DO.DeliveredStatus.Absent, DO.DeliveredStatus.Rejected, DO.DeliveredStatus.Failed };
                retryStatus = retryFailureStatuses[s_rand.Next(retryFailureStatuses.Length)];
                
                int failTimeMinutes = s_rand.Next(15, 35);
                retryArrival = retryPickup.AddMinutes(failTimeMinutes);
            }
            
            // Create the retry delivery
            Delivery retryDelivery = new Delivery
            (
                Id: 0,
                OrderId: orderIdToRetry,
                CourierId: retryCourier.Id,
                PickupTime: retryPickup,
                Transport: retryCourier.Transport,
                ArrivalTime: retryArrival,
                Distance: null,
                DeliveredStatus: retryStatus
            );
            
            s_dal!.Delivery.Create(retryDelivery);
            ordersToRetry.Remove(orderIdToRetry); // Don't retry the same order multiple times
            retryAttempts++;
            totalDeliveriesCreated++;
        }

        Console.WriteLine($"Created {totalDeliveriesCreated} deliveries with realistic retry scenarios:");
        Console.WriteLine($"  - {firstPassDeliveries} initial delivery attempts");
        Console.WriteLine($"  - {retryAttempts} retry attempts for failed/absent orders");
    }

    public static Adresses[] addresses = new Adresses[]
    {
        new Adresses("2, Kadish Luz, Holyland", 31.7587739, 35.1842047, 2.2, 2.6, 3.3),
        new Adresses("21 HaVaad Leumi israel",31.76577, 35.1931724, 1.5, 12.0, 3.7),
        new Adresses("31 HaRav Frank", 31.7633132, 35.186651, 1.1, 18.0, 2.5),
        new Adresses("73 HaRav Uziel", 31.7667718, 35.1858274, 0.9, 24.0, 1.8),
        new Adresses("87 Arieh Ben Eliezer", 31.7371065, 35.1969742, 1.9, 30.0, 3.3)
    };

    public static Adresses CompanyAdress = new Adresses("22 HaMeyasdim jerusalem", 31.7783596, 35.1871059, 0.0, 0.0, 0.0);

    public static void Do() 
    {
        s_dal = DalApi.Factory.Get;

        Console.WriteLine("Reset Configaration values and List values...");
        s_dal.ResetDB();

        // Initialize config to consistent values so BL AdminManager.Now is sane
        try
        {
            // set company address and coordinates (avoid (0,0))
            s_dal.Config.CompanyAdress = CompanyAdress.Street;
            s_dal.Config.Latitude = CompanyAdress.Latitude;
            s_dal.Config.Longitude = CompanyAdress.Longitude;

            // set the central clock to the current system time
            s_dal.Config.Clock = DateTime.Now;

            // Ajuster les paramètres pour avoir un équilibre réaliste
            if (s_dal.Config.MaxTimeDelivery == TimeSpan.Zero)
                s_dal.Config.MaxTimeDelivery = TimeSpan.FromMinutes(45); // 45 minutes
            if (s_dal.Config.RiskRange == TimeSpan.Zero)
                s_dal.Config.RiskRange = TimeSpan.FromMinutes(8); // 8 minutes before limit
        }
        catch
        {
            // best-effort, never crash init
        }

        Console.WriteLine("Initializing Delivery list...");
        createCouriers();
        createOrders();
        createDeliveries();
    }
}


