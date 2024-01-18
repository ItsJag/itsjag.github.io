using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class Enemy : MonoBehaviour {

    #region Properties
    protected Vector3 Localscale;
    protected PolygonCollider2D EndOfVision;

    //Patrol method properties

    protected float PatrolSpeed;
    protected Rigidbody2D EnemyRigidbody;
    protected Transform EdgeChecker;
    protected Transform CollisionChecker;
    protected Collider2D boxCollider;
    protected float RayLength;
    protected int LeftorRight;
    protected bool FacingRight = true;
    protected Animator animator;


    //Pathfinding Properties
    protected bool StartPathfinding;
    protected NewAStar aStar;
    protected List<Vector2> path;
    protected int currentWaypoint;
    protected bool pathIsEnded;
    protected float UpdateRate;
    protected float nextWaypointDistance;

    //Search Properties
    protected Transform Player;
    protected Vector2 PlayersLastPosition;
    protected Transform SearchPosition;
    protected Vector2 SearchPositionVector;

    //Avoid Properties
    protected Transform AvoidObject;
    protected int RunDirection;

    //Damage properties
    protected int Health;
    protected int MaxHealth;
    private GameManager Manager;


    //Difficulty Values
    protected int Difficulty;

    //New Properties
    protected enum EnemyStates
    {
        Patrol = 1, Follow = 2, Searching = 3, Avoid = 4

    }
    protected EnemyStates CurrentState;
    protected float TimeNotSeenFor;
    protected float SearchTimeThreshold;

    #endregion

    // Use this for initialization
    // Use this for initialization
    protected virtual void Start()
    {
        PropertyInitialisation();

        //New bit
        NewState = EnemyStates.Patrol;

    }

    // Update is called once per frame
    protected virtual void Update()
    {
        IsLeftOrRight();


        //Maybe replace EndOfVision transform with a long collider instead so it runs on collision instead of every frame.
        IfPlayerSeen();


        switch (CurrentState)
        {
            case EnemyStates.Patrol:
                Patrol();
                break;
            case EnemyStates.Follow:
                Follow();
                break;
            case EnemyStates.Searching:
                Search();
                break;
            case EnemyStates.Avoid:
                Avoid();
                break;
            default:
                Patrol();
                break;
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player" && !Physics2D.Linecast(transform.position, Player.position, 1 << LayerMask.NameToLayer("Environment Tiles")))
        {
            if (CurrentState == EnemyStates.Follow)
            {
                //Resets this property as the Enemy has just "seen" the Player
                TimeNotSeenFor = 0;
            }
            else
            {
                //Transition from Patrol/Searching to Follow
                NewState = EnemyStates.Follow;
            }
        }

        if (collision.gameObject.tag == "Projectile" && Physics2D.Linecast(transform.position, collision.transform.position, 1 << LayerMask.NameToLayer("Environment Tiles")).distance > 3)
        {
            AvoidObject = collision.transform;
            NewState = EnemyStates.Avoid;
        }
    }

    protected virtual void IfPlayerSeen()
    {
        
        if (CurrentState == EnemyStates.Follow && TimeNotSeenFor >= SearchTimeThreshold)
        {
            //Transition from Follow to Searching
            TimeNotSeenFor = 0;
            PlayersLastPosition =  GameObjectFinder.GetSetPlayer.transform.position;
            bool SafeToSearch = CheckSurroundings(PlayersLastPosition);
            if (SafeToSearch)
            {
                SearchPosition.position = PlayersLastPosition;
                SearchPositionVector = SearchPosition.position;
                NewState = EnemyStates.Searching;
            }
            else
            {
                int Tries = 0;
                do
                {
                    System.Random randomDistance = new System.Random(DateTime.Now.Millisecond);

                    SearchPosition.position = new Vector2(PlayersLastPosition.x + randomDistance.Next(-10, 10), PlayersLastPosition.y + randomDistance.Next(-4, 4));
                    SearchPositionVector = SearchPosition.position;
                    if (CheckSurroundings(SearchPosition.position))
                    {
                        SafeToSearch = true;
                    }
                    else
                    {
                        Tries++;
                    }

                } while (!SafeToSearch && Tries < 5);
                if (Tries >= 5)
                {
                    NewState = EnemyStates.Patrol;
                }
                else if (SafeToSearch)
                {
                    Debug.Log("Switching to Search State.");
                    NewState = EnemyStates.Searching;
                }
            }
                
        }
        else if (CurrentState == EnemyStates.Follow || CurrentState == EnemyStates.Searching)
        {
            TimeNotSeenFor += Time.deltaTime;
        }
        else if (CurrentState == EnemyStates.Searching && TimeNotSeenFor >= SearchTimeThreshold)
        {
            NewState = EnemyStates.Patrol;
        }
    }


    protected virtual void IsLeftOrRight()
    {
        if (transform.localScale == Localscale)
        {
            FacingRight = true;
        }
        else
        {
            FacingRight = false;
        }

        if (FacingRight)
        {
            LeftorRight = 1;
        }
        else
        {
            LeftorRight = -1;
        }
    }

    //Since every type of enemy uses the patrol script, why not put it in the superclass?


    protected virtual void Turn()
    {
        FacingRight = !FacingRight;
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    protected virtual void CheckForEdge()
    {
        if (CurrentState == EnemyStates.Patrol && Physics2D.Raycast(EdgeChecker.position, Vector2.down, RayLength, 1 << LayerMask.NameToLayer("Environment Tiles")))
        {
            Vector2 TargetVelocity = new Vector2(PatrolSpeed * Time.fixedDeltaTime * 10f * LeftorRight, EnemyRigidbody.velocity.y);

            EnemyRigidbody.velocity = TargetVelocity;
            //Debug.Log("Turning because of CheckForEdge()");
        }
        else
        {
            Turn();
        }
    }

    protected virtual void CheckForCollision()
    {
        if (CurrentState == EnemyStates.Patrol && Physics2D.Linecast(transform.position, CollisionChecker.position, 1 << LayerMask.NameToLayer("Environment Tiles")))
        {
            Debug.Log("Turning because of CheckForCollision()");
            Turn();
        }
    }

    protected virtual void PropertyInitialisation()
    {
        StartPathfinding = true;
        aStar = new NewAStar();
    
        SearchPosition = transform.Find("SearchPos");
        Difficulty = OptionsClass.difficulty;

        switch (Difficulty)
        {
            case (int)OptionsClass.Difficulty.Easy:
                PatrolSpeed = 90;
                RayLength = 4;
                Health = 80;
                MaxHealth = 80;
                SearchTimeThreshold = 3;
                break;
            case (int)OptionsClass.Difficulty.Normal:
                PatrolSpeed = 100;
                RayLength = 7;
                Health = 100;
                MaxHealth = 100;
                SearchTimeThreshold = 5;
                break;
            case (int)OptionsClass.Difficulty.Hard:
                PatrolSpeed = 110;
                RayLength = 8;
                Health = 110;
                MaxHealth = 110;
                SearchTimeThreshold = 7;
                break;
            case (int)OptionsClass.Difficulty.Extreme:
                PatrolSpeed = 120;
                RayLength = 10;
                Health = 140;
                MaxHealth = 140;
                SearchTimeThreshold = 10;
                break;
            default:
                PatrolSpeed = 100;
                RayLength = 7;
                Health = 100;
                MaxHealth = 100;
                SearchTimeThreshold = 5;
                break;
        }
        
        EndOfVision = GetComponentInChildren<Transform>().Find("EndOfVision").GetComponent<PolygonCollider2D>();
        Localscale = transform.localScale;


        EnemyRigidbody = GetComponent<Rigidbody2D>();
        EdgeChecker = GetComponentInChildren<Transform>().Find("Edge_Checker");
        boxCollider = GetComponent<Collider2D>();
        CollisionChecker = GetComponentInChildren<Transform>().Find("Collision_Checker");
        animator = GetComponent<Animator>();

        Manager = GameObjectFinder.GetSetManager;

        TimeNotSeenFor = 0;


    }

    //These have to be virtual because abstract methods can only be in abstract classes
    #region StateMethods
    protected abstract void Follow();
    protected abstract void Patrol();
    protected abstract void Search();
    protected abstract void Avoid();
    #endregion

    #region StateMethodsInitialisers
    protected abstract void FollowInit();
    protected abstract void PatrolInit();
    protected abstract void SearchInit();
    protected abstract void AvoidInit();
    #endregion

    protected virtual void MoveTowardsNextWaypoint(Vector2 Direction)
    {
        Debug.Log("Teleported to " + new Vector2(Direction.x, transform.position.y) + " because of MoveTowards`NextWaypoint");
        transform.position = new Vector2(Direction.x, transform.position.y);
    }

    protected void Pathfind(Vector2 EndOfPath)
    {
        //This makes sure that the coroutine cannot be called too often to improve performance, and to also make sure 
        //two instances of UpdatePath() aren't running simultaneously.
        if (StartPathfinding)
        {
            StartCoroutine(UpdatePath(EndOfPath));
            StartPathfinding = false;
        }
        

        //If the path doesn't exist, then there's no point in continuing this method.
        if (path == null)
        {
            return;
        }
        //If the currentWaypoint (index within the path's array) is too big, then the path has been traversed.
        if (currentWaypoint >= path.Count)
        {
            pathIsEnded = true;
            Vector2 direction = ((Vector2)Player.position - (Vector2)transform.position).normalized;
            ComputePathfindOutput(direction);
            return;
        }
        else
        {
            pathIsEnded = false;
        }
        //A unit vector of the direction the enemy has to travel, to reach the next waypoint.
        Vector2 dir = (path[currentWaypoint] - (Vector2)transform.position).normalized;

        float distance = Vector2.Distance(transform.position, path[currentWaypoint]);
        //If the distance between the enemy and it's next waypoint is small, then it skips to the next waypoint.

        if (distance <= nextWaypointDistance)
        {
            currentWaypoint++;
        }
        //This method tells the individual type of enemy what to do with this data.
        ComputePathfindOutput(dir);
    }

    protected IEnumerator UpdatePath(Vector2 EndOfPath)
    {
        if (aStar.IsDone())
        {
            path = aStar.GeneratePath(transform.position, EndOfPath);
        }
        yield return new WaitForSeconds(1 / UpdateRate);
        //This here used to say StartCoroutine(UpdatePath(EndOfPath)) but since the previous version of this used Transform, when it called position, it updated with the engine. But a Vector2 cannot update in the same way as a Transform.position Vector
        StartPathfinding = true;
    }

    protected abstract void ComputePathfindOutput(Vector2 direction);

    protected virtual bool CheckSurroundings(Vector2 PositionToBeChecked)
    {
        if (Physics2D.Raycast(PositionToBeChecked, Vector2.down, 10f, 1 << LayerMask.NameToLayer("Environment Tiles")) || Physics2D.Raycast(PositionToBeChecked, Vector2.down, 4f, 1 << LayerMask.NameToLayer("Enemies")))
        {
            Debug.Log("Surroundings return true");
            return true;

        }
        else
        {
            return false;
        }
    }

    protected void Die()
    {
        Manager.GetSetPoints += 500;
        Manager.IncrementEnemiesDestroyed();
        Destroy(this.gameObject);
    }

    protected void CheckIfDead()
    {
        if (Health <= 0)
        {
            animator.SetBool("Dead", true);

        }
    }

    public int GetSetHealth
    {
        get
        {
            return Health;
        }

        set
        {
            if (Health > value)
            {
                Health = value;
            }
            CheckIfDead();
            NewState = EnemyStates.Follow;

        }
    }

    protected virtual EnemyStates NewState
    {
        set
        {
            StartPathfinding = true;
            TimeNotSeenFor = 0;
            CurrentState = value;
            switch (CurrentState)
            {
                case EnemyStates.Patrol:
                    PatrolInit();
                    break;
                case EnemyStates.Follow:
                    FollowInit();
                    break;
                case EnemyStates.Searching:
                    SearchInit();
                    break;
                case EnemyStates.Avoid:
                    AvoidInit();
                    break;
                default:
                    PatrolInit();
                    break;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (CurrentState == EnemyStates.Follow && path != null)
        {
            if (currentWaypoint < path.Count)
            {
                Debug.DrawLine(transform.position, path[currentWaypoint], Color.green);
            }
            
        }

    }
}










