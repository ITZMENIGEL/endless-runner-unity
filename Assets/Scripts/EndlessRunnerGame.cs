using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndlessRunnerGame : MonoBehaviour
{
    const float TileLength = 30f;
    const int TileCount = 8;
    const float TrackWidth = 9f;
    readonly float[] lanes = { -3f, 0f, 3f };

    GameObject player;
    Rigidbody playerBody;
    Camera gameCamera;
    Text scoreText;
    Text messageText;
    readonly List<GameObject> tiles = new List<GameObject>();
    readonly List<GameObject> hazards = new List<GameObject>();
    readonly List<GameObject> coins = new List<GameObject>();
    int laneIndex = 1;
    int score;
    int coinsCollected;
    int bestScore;
    float speed = 10f;
    float distance;
    float spawnZ = 35f;
    float nextObstacleZ = 35f;
    float nextCoinZ = 28f;
    bool running = true;
    bool jumping;
    Vector2 touchStart;

    Material groundMat, sideMat, playerMat, hazardMat, coinMat;

    void Awake()
    {
        if (FindObjectOfType<EndlessRunnerGame>() != this) return;
        BuildGame();
    }

    void BuildGame()
    {
        bestScore = PlayerPrefs.GetInt("EndlessRunnerBest", 0);
        Application.targetFrameRate = 60;
        CreateMaterials();
        CreateEnvironment();
        CreatePlayer();
        CreateCamera();
        CreateUI();
    }

    void CreateMaterials()
    {
        groundMat = Material("Track", new Color(.08f, .11f, .16f));
        sideMat = Material("Edge", new Color(.03f, .5f, .7f));
        playerMat = Material("Runner", new Color(.15f, .85f, 1f));
        hazardMat = Material("Hazard", new Color(1f, .18f, .12f));
        coinMat = Material("Coin", new Color(1f, .75f, .05f));
    }

    Material Material(string name, Color color)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.name = name; mat.color = color;
        mat.SetFloat("_Glossiness", .65f);
        return mat;
    }

    void CreateEnvironment()
    {
        for (int i = 0; i < TileCount; i++)
        {
            var tile = Cube("Track Tile", new Vector3(0, -.35f, i * TileLength), new Vector3(TrackWidth, .5f, TileLength), groundMat);
            tiles.Add(tile);
        }
        Cube("Left Rail", new Vector3(-5f, .15f, 105f), new Vector3(.25f, 1f, 270f), sideMat);
        Cube("Right Rail", new Vector3(5f, .15f, 105f), new Vector3(.25f, 1f, 270f), sideMat);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(.025f, .04f, .08f);
        RenderSettings.fogDensity = .008f;
        var light = new GameObject("Moon Light").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.1f;
        light.color = new Color(.65f, .8f, 1f); light.transform.rotation = Quaternion.Euler(45, -30, 0);
    }

    void CreatePlayer()
    {
        player = Capsule("Player", new Vector3(0, 1.1f, 0), new Vector3(.9f, 1.8f, .9f), playerMat);
        playerBody = player.AddComponent<Rigidbody>();
        playerBody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX;
        playerBody.interpolation = RigidbodyInterpolation.Interpolate;
        playerBody.mass = 2f;
        player.tag = "Player";
    }

    void CreateCamera()
    {
        gameCamera = new GameObject("Main Camera").AddComponent<Camera>();
        gameCamera.tag = "MainCamera";
        gameCamera.fieldOfView = 65f;
        gameCamera.transform.position = new Vector3(0, 5.5f, -9f);
        gameCamera.transform.rotation = Quaternion.Euler(15f, 0, 0);
    }

    void CreateUI()
    {
        var canvas = new GameObject("HUD").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        var panel = new GameObject("Score"); panel.transform.SetParent(canvas.transform, false);
        scoreText = panel.AddComponent<Text>(); scoreText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        scoreText.fontSize = 34; scoreText.color = Color.white; scoreText.alignment = TextAnchor.UpperLeft;
        scoreText.rectTransform.anchorMin = new Vector2(0, 1); scoreText.rectTransform.anchorMax = new Vector2(0, 1);
        scoreText.rectTransform.pivot = new Vector2(0, 1); scoreText.rectTransform.anchoredPosition = new Vector2(35, -30);
        scoreText.rectTransform.sizeDelta = new Vector2(800, 100);
        var msg = new GameObject("Message"); msg.transform.SetParent(canvas.transform, false);
        messageText = msg.AddComponent<Text>(); messageText.font = scoreText.font; messageText.fontSize = 30; messageText.color = Color.white; messageText.alignment = TextAnchor.MiddleCenter;
        messageText.rectTransform.anchorMin = new Vector2(.5f, .5f); messageText.rectTransform.anchorMax = new Vector2(.5f, .5f); messageText.rectTransform.sizeDelta = new Vector2(900, 220);
        messageText.text = "ENDLESS RUNNER\nA/D or arrows to change lanes  •  SPACE to jump";
        Invoke(nameof(HideMessage), 3.5f);
    }

    void HideMessage() { if (messageText != null) messageText.text = ""; }

    void Update()
    {
        if (!running) { if (Input.GetKeyDown(KeyCode.R)) Restart(); return; }
        ReadInput();
        speed += Time.deltaTime * .18f;
        distance += speed * Time.deltaTime;
        score = Mathf.FloorToInt(distance * 2f) + coinsCollected * 25;
        scoreText.text = "SCORE  " + score + "\nCOINS  " + coinsCollected + "    BEST  " + Mathf.Max(bestScore, score);
        SpawnWorld();
        RecycleTrack();
        UpdateCamera();
    }

    void FixedUpdate()
    {
        if (!running) return;
        playerBody.velocity = new Vector3(0, playerBody.velocity.y, speed);
        var p = player.transform.position;
        float desiredX = lanes[laneIndex];
        p.x = Mathf.Lerp(p.x, desiredX, Time.fixedDeltaTime * 12f);
        player.transform.position = p;
        if (player.transform.position.y < -2f) Crash();
    }

    void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) laneIndex = Mathf.Max(0, laneIndex - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) laneIndex = Mathf.Min(2, laneIndex + 1);
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) && !jumping)
        { playerBody.AddForce(Vector3.up * 8.5f, ForceMode.VelocityChange); jumping = true; }
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) touchStart = t.position;
            if (t.phase == TouchPhase.Ended)
            {
                Vector2 d = t.position - touchStart;
                if (Mathf.Abs(d.x) > Mathf.Abs(d.y) && Mathf.Abs(d.x) > 40) laneIndex = Mathf.Clamp(laneIndex + (d.x > 0 ? 1 : -1), 0, 2);
                else if (d.y > 40 && !jumping) { playerBody.AddForce(Vector3.up * 8.5f, ForceMode.VelocityChange); jumping = true; }
            }
        }
    }

    void OnCollisionEnter(Collision c)
    {
        if (c.gameObject.CompareTag("Hazard")) Crash();
        if (c.gameObject.CompareTag("Ground")) jumping = false;
    }

    void SpawnWorld()
    {
        while (nextObstacleZ < player.transform.position.z + 150f)
        {
            int lane = Random.Range(0, 3); float z = nextObstacleZ;
            var h = Cube("Obstacle", new Vector3(lanes[lane], 1f, z), new Vector3(1.8f, 2f, 1.5f), hazardMat);
            h.tag = "Hazard"; hazards.Add(h);
            if (Random.value > .35f) CreateCoin(Random.Range(0, 3), z - 7f);
            nextObstacleZ += Random.Range(12f, 20f);
        }
        while (nextCoinZ < player.transform.position.z + 150f)
        {
            CreateCoin(Random.Range(0, 3), nextCoinZ); nextCoinZ += Random.Range(7f, 13f);
        }
    }

    void CreateCoin(int lane, float z)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = "Coin"; c.transform.position = new Vector3(lanes[lane], 1.4f, z); c.transform.localScale = new Vector3(.7f, .12f, .7f); c.transform.rotation = Quaternion.Euler(90, 0, 0); c.GetComponent<Renderer>().material = coinMat;
        c.GetComponent<Collider>().isTrigger = true; c.AddComponent<CoinPickup>().game = this; coins.Add(c);
    }

    public void Collect(GameObject coin) { coinsCollected++; coins.Remove(coin); Destroy(coin); }

    void RecycleTrack()
    {
        foreach (var tile in tiles) if (tile.transform.position.z + TileLength / 2 < player.transform.position.z - 25f)
            tile.transform.position += Vector3.forward * TileLength * TileCount;
        hazards.RemoveAll(x => { if (x == null) return true; if (x.transform.position.z < player.transform.position.z - 30) { Destroy(x); return true; } return false; });
        coins.RemoveAll(x => { if (x == null) return true; if (x.transform.position.z < player.transform.position.z - 30) { Destroy(x); return true; } return false; });
    }

    void UpdateCamera()
    {
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, player.transform.position + new Vector3(0, 4.7f, -8f), Time.deltaTime * 5f);
        gameCamera.transform.LookAt(player.transform.position + Vector3.forward * 12f + Vector3.up * .5f);
    }

    void Crash()
    {
        if (!running) return; running = false; bestScore = Mathf.Max(bestScore, score); PlayerPrefs.SetInt("EndlessRunnerBest", bestScore); PlayerPrefs.Save();
        messageText.text = "CRASHED!\nScore: " + score + "\nPress R to run again";
    }

    void Restart() { UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); }

    GameObject Cube(string n, Vector3 pos, Vector3 scale, Material mat) { var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = n; o.transform.position = pos; o.transform.localScale = scale; o.GetComponent<Renderer>().material = mat; if (n.Contains("Track")) o.tag = "Ground"; return o; }
    GameObject Capsule(string n, Vector3 pos, Vector3 scale, Material mat) { var o = GameObject.CreatePrimitive(PrimitiveType.Capsule); o.name = n; o.transform.position = pos; o.transform.localScale = scale; o.GetComponent<Renderer>().material = mat; return o; }
}

public class CoinPickup : MonoBehaviour
{
    public EndlessRunnerGame game;
    void Update() { transform.Rotate(0, 180f * Time.deltaTime, 0); }
    void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) game.Collect(gameObject); }
}
