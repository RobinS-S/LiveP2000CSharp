using System;

namespace LiveP2000CSharp
{
    public enum P2000ServiceType
    {
        Brandweer = 1,
        Ambulance = 2,
        Politie = 3,
        KNRM = 4,
        AmbulanceHelicopter = 5
    }
    public class P2000Alert
    {
        public DateTime Time { get; set; }
        public P2000ServiceType Service { get; set; }
        public P2000Capcode[] Capcodes { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public bool IsPriority { get; set; }
        public string Message { get; set; }
    }
    public class P2000Capcode
    {
        public string UnitName { get; set; }
        public int Capcode { get; set; }
    }
    enum LiveP2000PacketType
    {
        /// <summary>connection Close Event</summary>
        CNCe = 0,
        /// <summary>connection Open Event</summary>
        CNOe = 1,
        /// <summary>room join Event</summary>
        JOIe = 2,
        /// <summary>room leave event</summary>
        LEAe = 3,
        /// <summary>room open event</summary>
        ROOe = 4,
        /// <summary>room close event</summary>
        ROCe = 5,
        /// <summary>Authentification request (client -> master)</summary>
        AUTq = 6,
        /// <summary>Authentification response (master -> client)</summary>
        AUTr = 7,
        /// <summary>Authentification request by PIN</summary>
        AUPq = 8,
        /// <summary>Authentification request by COOKIE</summary>
        AUCq = 9,
        /// <summary>Anno authentification request</summary>
        AUAq = 10,
        /// <summary>Command First Request</summary>
        FRQc = 11,
        /// <summary>data request</summary>
        DATq = 12,
        /// <summary>data response</summary>
        DATr = 13,
        /// <summary>Request for mapdata</summary>
        MAPq = 14,
        /// <summary>Response with mapdata</summary>
        MAPr = 15,
        /// <summary>request for 'woordenboek'</summary>
        WDBq = 16,
        /// <summary>resposne with 'woordenboek'</summary>
        WDBr = 17,
        /// <summary>manual request to logof</summary>
        LGFq = 18,
        /// <summary>command to leave a room</summary>
        LEAc = 19,
        /// <summary>command to join a room</summary>
        JOIc = 20,
        /// <summary>request capcode history</summary>
        CAPq = 21,
        /// <summary>response with capcode history</summary>
        CAPr = 22,
        /// <summary>command to re start gracefull way</summary>
        GRAc = 25,
        /// <summary>unlock command</summary>
        STAc = 29,
        /// <summary>client ready to recieve data</summary>
        RDYr = 30,
        /// <summary>request for 'city's'</summary>
        CTYq = 31,
        /// <summary>resposne with 'city's'</summary>
        CTYr = 32,
        /// <summary>preserved for server side reporter</summary>
        RDWe = 33,
        /// <summary>request for single SPI</summary>
        SPIq = 34,
        /// <summary>resposne for single SPI</summary>
        SPIr = 35,
        /// <summary>request for 'city names'</summary>
        CTRq = 36,
        /// <summary>resposne with 'city names'</summary>
        CTRr = 37,
        /// <summary>Ping request</summary>
        PINq = 38,
        /// <summary>Ping response</summary>
        PINr = 39,
    }
}