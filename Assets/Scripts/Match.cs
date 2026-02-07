using UnityEngine;
using GameData;
using GameMap;

// Placeholder
namespace GameManager {
    public class Match : MonoBehaviour {
        public void Start() {
            // Create parent objects for allies and enemies
            GameObject allies = new GameObject("Allies");
            GameObject enemies = new GameObject("Enemies");

            // Create test agents
            GameObject newAlly = Agent.NewAgent(
                player: null,
                agent_name: "Test Ally 1",
                hp: 30,
                range: 3,
                abilities: null,
                tunneling: Agent.Tunneling.Ally,
                gameObjectName: null,
                parent: allies,
                position: new Vector3(2, 0, 0)
            );
            GameObject newAgent = Agent.NewAgent(
                player: null,
                agent_name: "Test Enemy 1",
                hp: 15,
                range: 2,
                abilities: null,
                tunneling: Agent.Tunneling.Nothing,
                gameObjectName: null,
                parent: enemies,
                position: new Vector3(0, 0, 0)
            );

            // Add sphere mesh to both agents
            MeshFilter enemyMesh = newAgent.AddComponent<MeshFilter>();
            enemyMesh.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            MeshFilter allyMesh = newAlly.AddComponent<MeshFilter>();
            allyMesh.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            // Make blue material
            Material blueMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            blueMaterial.color = Color.blue;
            // Make red material
            Material redMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            redMaterial.color = Color.red;

            // Apply blue material to ally agent
            MeshRenderer allyRenderer = newAlly.AddComponent<MeshRenderer>();
            allyRenderer.materials = new Material[] { blueMaterial };
            // Apply red material to enemy agent
            MeshRenderer enemyRenderer = newAgent.AddComponent<MeshRenderer>();
            enemyRenderer.materials = new Material[] { redMaterial };
        }
    }
}
