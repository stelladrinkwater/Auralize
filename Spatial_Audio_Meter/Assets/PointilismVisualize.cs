using UnityEngine;
using System.Collections.Generic;
using extOSC;

public class PointilismVisualize : MonoBehaviour
{
    public GameObject pointTemplate;
    public int maxPoints = 50;
    public float scaleFactor = 5.0f;
    public float pointSizeMultiplier = 0.5f;
    public float decayRate = 0.95f;
    public int oscPort = 7001;
    
    private Dictionary<int, PointInfo> points = new Dictionary<int, PointInfo>();
    private OSCReceiver receiver;
    
    [System.Serializable]
    public class PointInfo
    {
        public GameObject obj;
        public float energy;
        public float targetEnergy;
        public Vector3 targetPosition;
    }
    
    void Start()
    {
        Debug.Log("Starting AmbisonicVisualizer");
        
        // hide the template
        pointTemplate.SetActive(false);
        
        // setup osc receiver
        receiver = gameObject.AddComponent<OSCReceiver>();
        receiver.LocalPort = oscPort;
        
        // bind to messages
        receiver.Bind("/point/*", OnPointMessage);
        
        Debug.Log("OSC Receiver started on port: " + oscPort);
        
        // create all the point objects
        for (int i = 0; i < maxPoints; i++)
        {
            GameObject newPoint = Instantiate(pointTemplate, Vector3.zero, Quaternion.identity, transform);
            newPoint.name = "Point_" + i;
            newPoint.SetActive(false);
            
            points.Add(i, new PointInfo {
                obj = newPoint,
                energy = 0f,
                targetEnergy = 0f,
                targetPosition = Vector3.zero
            });
            
            Debug.Log("Created point: " + newPoint.name);
        }
    }
    
    void OnPointMessage(OSCMessage message)
    {
        // get the point id from the address
        string address = message.Address;
        string idPart = address.Substring(address.LastIndexOf('/') + 1);
        
        // try to parse it as a number
        if (int.TryParse(idPart, out int pointId))
        {
            // check if we have this point
            if (pointId < maxPoints && points.ContainsKey(pointId))
            {
                // make sure message has all values
                if (message.Values.Count >= 4)
                {
                    // get coordinates and energy
                    float x = message.Values[0].FloatValue;
                    float y = message.Values[1].FloatValue;
                    float z = message.Values[2].FloatValue;
                    float energy = message.Values[3].FloatValue;
                    
                    // update our point
                    PointInfo point = points[pointId];
                    point.targetPosition = new Vector3(x, y, z) * scaleFactor;
                    point.targetEnergy = energy;
                    point.obj.SetActive(true);
                    
                    Debug.Log($"Updated point {pointId}: pos=({x},{y},{z}), energy={energy}");
                }
            }
        }
    }
    
    void Update()
    {
        // update all points
        foreach (var pair in points)
        {
            int id = pair.Key;
            PointInfo point = pair.Value;
            
            // smooth energy transition
            point.energy = Mathf.Lerp(point.energy, point.targetEnergy, Time.deltaTime * 10f);
            
            // decay energy over time
            point.targetEnergy *= decayRate;
            
            // smooth position transition
            point.obj.transform.position = Vector3.Lerp(
                point.obj.transform.position, 
                point.targetPosition, 
                Time.deltaTime * 8f
            );
            
            // update size based on energy
            float size = point.energy * pointSizeMultiplier;
            point.obj.transform.localScale = new Vector3(size, size, size);
            
            // hide if too small
            if (point.energy < 0.01f)
            {
                point.obj.SetActive(false);
            }
        }
    }
}