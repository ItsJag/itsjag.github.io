using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class Player : MonoBehaviour {

    //Declaration of all the Movement related variables
    #region Movement Variables
    private Animator animator;
    private Rigidbody2D PlayerRigidBody;
    private BoxCollider2D PlayerBoxCollider;
    private float WalkingSpeed;
    private float JumpingSpeed;
    private float JumpingSpeedInitialValue;
    private Transform GroundCheck;
    private Transform WallStickGroundCheck;
    private bool FacingRight;
    private bool Grounded;
    private bool LookingUp;
    private float ActualSpeed;
    private float LeftorRight;
    private bool IsRunning;
    private float RunningSpeedMultiplier;
    private float SpeedBoostMultiplier;
    private float JumpTimerThreshold;
    private float JumpTimer;
    private bool SprintToggle;
    #endregion

    //All these vectors and transforms below are related to the different player states (Ducking, LookingUp, and StoodUp)
    #region AnimationStates Variables
    private Vector3 ProjectileOriginStoodUp;
    private Vector3 ProjectileOriginDucked;
    private Vector3 ProjectileOriginLookUp;
    private Transform ProjectileOrigin;
    private Vector2 StoodUpColliderOffset;
    private Vector2 DuckedColliderOffset;
    private Vector2 StoodUpColliderSize;
    private Vector2 DuckedColliderSize;
    private bool Ducked;
    private bool RunAntiStick;
    #endregion

    //These are the variables for Projectile_Damage_and_Points
    #region Projectile_Damage_and_Points Variables
    private int Health = 100;
    //private GameObject Weapon;
    private PlayerWeapons Weapon;
    private const string ProjectileName = "Bullet";
    private static bool TakeDamage;
    private bool AllowedToTakeDamage;
    private int Points;
    private GameManager Manager;
    private float DamageMultiplier;
    private Rifle rifle;
    private Shotgun shotgun;
    private Bazooka bazooka;
    private Sniper sniper;
    private float RateOfFire;
    private float RateOfFireTimer;
    #endregion

    // Use this for initialization
    void Start()
    {
        
        GameObjectFinder.GetSetPlayer = this;
        //This initialises the DamageandPointsProperties
        DamageAndPointsPropertiesInit();
        //These are all the local positions used when changing the position of the projectile origin (where all bullets/projectiles come out on the player's sprite) when you are ducking, stood up or looking up. 
        ProjectileOriginInitialisation();

        //Sets values of properties related to Player Speed, animation booleans, etc.
        PlayerMovementInitialisation();

        //This initialises the different collider sizes and positions that occur when Ducking, Stood up and when Looking Up.
        ColliderInitialisation();

    }

    // Update is called once per frame
    void Update()
    {
        //These two methods contain all code ran each frame for the PlayerCharacter.
        DamageAndPointsUpdate();
        MovementUpdate();
    }

    #region Movement Methods
    void Move()
    {
        if (!Ducked)
        {
            PlayerRigidBody.velocity = new Vector3(ActualSpeed * Time.fixedDeltaTime * 10f * LeftorRight * SpeedBoostMultiplier, PlayerRigidBody.velocity.y);
        }
    }

    void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }


    /// <summary>
    /// This method returns values to the Player's Animator so it can have animations playing that represent the player's current actions.
    /// </summary>
    void AnimatorChecks()
    {
        if (Grounded)
        {
            animator.SetBool("Grounded", true);
        }
        else
            animator.SetBool("Grounded", false);

        if (PlayerRigidBody.velocity.x != 0)
        {
            animator.SetBool("Moving", true);
        }
        else
            animator.SetBool("Moving", false);

        if (!Grounded && PlayerRigidBody.velocity.y < 0)
        {
            animator.SetBool("Falling", true);
        }
        else if (!Grounded && PlayerRigidBody.velocity.y > 0)
        {
            animator.SetBool("Falling", false);
        }
        if (IsRunning)
        {
            animator.SetBool("Running", true);
        }
        else
        {
            animator.SetBool("Running", false);
        }
        animator.SetFloat("Speed", Mathf.Abs(Input.GetAxis("Horizontal")));
    }


    /// <summary>
    /// This small method is made to prevent the player from sticking to walls when walking whilst jumping.
    /// </summary>
    /// <param name="IsEntering"> IsEntering means the program checks whether it's entering a collision, or exiting it.</param>
    void WallAntiStick()
    {
        //Since this function has quite a few checks in it, this now only runs when a collision is occurring, rather than running every frame
        //Need to make this only run when moving in the direction of the wall you're colliding with

        if (!(PlayerBoxCollider.IsTouchingLayers(1 << LayerMask.NameToLayer("Environment Tiles"))))
        {
            //If the collider is not the intended collider, it doesn't even run the rest of the method
            return;
        }

        if (PlayerBoxCollider.IsTouchingLayers(1 << LayerMask.NameToLayer("Environment Tiles")) && LeftorRight != 0 && !(Physics2D.Linecast(transform.position, WallStickGroundCheck.position, 1 << LayerMask.NameToLayer("Environment Tiles"))))
        {
            //Debugged this, for some reason only runs once when collided
            PlayerRigidBody.velocity = new Vector2(PlayerRigidBody.velocity.x * Time.fixedDeltaTime, PlayerRigidBody.velocity.y);
            animator.SetBool("WallStuck", true);
        }
        else
            animator.SetBool("WallStuck", false);
    }


    /// <summary>
    /// This does a check whether the player is facing in the correct direction by making a comparison between the Input.GetAxis("Horizontal") and the FacingRight bool.
    /// </summary>
    void CheckIfIShouldFlip()
    {
        if (LeftorRight > 0 && !FacingRight)
            Flip();
        else if (LeftorRight < 0 && FacingRight)
            Flip();

    }


    /// <summary>
    /// This checks if the player is currently grounded and if so, then they are able to jump.
    /// </summary>
    void CanIJump()
    {
        AmIGrounded();
        if (Input.GetKey(KeyBindsClass.Jump.keyCode) && Grounded)
        {
            JumpTimer = 0;
        }

        if (Input.GetKey(KeyBindsClass.Jump.keyCode) && JumpTimer <= JumpTimerThreshold)
        {
            PlayerRigidBody.velocity = new Vector2(PlayerRigidBody.velocity.x, JumpingSpeed * Time.fixedDeltaTime * 6f);
            JumpTimer += Time.deltaTime;
        }
    }


    void ShouldIMove()
    {
        if (LeftorRight != 0)
        {
            Move();

        }
    }

    /// <summary>
    /// This function's purpose is to check whether the player is currently on the ground, used for Animation changes and also to check whether the player is able to shoot or jump.
    /// </summary>
    void AmIGrounded()
    {
        if (Physics2D.Linecast(transform.position, GroundCheck.position, 1 << LayerMask.NameToLayer("Environment Tiles")) || Physics2D.Linecast(transform.position, GroundCheck.position, 1 << LayerMask.NameToLayer("Enemies")))
        {
            Grounded = true;
        }
        else
        {
            Grounded = false;
        }
    }

    /// <summary>
    /// This Method's whole purpose is to change the walking/running speed of the character depending on the context of the action, like if they are running, they should be moving ~1.4 times faster.
    /// </summary>
    void AlterRunningSpeed()
    {
        if (OptionsClass.sprintToggle)
        {
            if (Input.GetKeyDown(KeyBindsClass.Sprint.keyCode))
            {
                SprintToggle = !SprintToggle;
            }

            if (Grounded && SprintToggle && LeftorRight != 0)
            {
                ActualSpeed = WalkingSpeed * RunningSpeedMultiplier * SpeedBoostMultiplier;
                IsRunning = true;
            }
            else
            {
                IsRunning = false;
            }
        }
        else
        {
            if (Grounded && Input.GetKey(KeyBindsClass.Sprint.keyCode) && LeftorRight != 0)
            {
                ActualSpeed = WalkingSpeed * RunningSpeedMultiplier * SpeedBoostMultiplier;
                IsRunning = true;
            }
            else
            {
                IsRunning = false;
            }
        }


        
    }


    /// <summary>
    /// This sets the values if the player is ducking or not.
    /// </summary>
    /// <param name="IsDucked"> a bool that describes whether the player is ducking or not.</param>
    void Duck(bool IsDucked)
    {
        if (IsDucked)
        {
            //Set new transform positions of projectile origin, etc.
            animator.SetBool("Ducking", true);
            ProjectileOrigin.transform.localPosition = ProjectileOriginDucked;
            PlayerBoxCollider.offset = DuckedColliderOffset;
            PlayerBoxCollider.size = DuckedColliderSize;
            Ducked = true;



        }
        else if (IsDucked == false)
        {
            //Set transform positions of projectile origin to the original value
            animator.SetBool("Ducking", false);
            ProjectileOrigin.transform.localPosition = ProjectileOriginStoodUp;
            PlayerBoxCollider.offset = StoodUpColliderOffset;
            PlayerBoxCollider.size = StoodUpColliderSize;
            Ducked = false;
        }
    }

    private void PlayerMovementInitialisation()
    {
        WalkingSpeed = 200;
        JumpingSpeed = 300;
        JumpingSpeedInitialValue = JumpingSpeed;
        RunningSpeedMultiplier = 1.3f;
        ActualSpeed = WalkingSpeed;
        PlayerRigidBody = GetComponent<Rigidbody2D>();
        PlayerBoxCollider = GetComponent<BoxCollider2D>();
        GroundCheck = GetComponentInChildren<Transform>().Find("GroundCheck");
        WallStickGroundCheck = GetComponentInChildren<Transform>().Find("WallStickGroundCheck");
        //This FacingRight bool is used for many different things: 1. To calculate if movementspeed should be *1 or *-1. 2. To check which direction the Projectiles (bullets) should travel from your sprite. 3. Which direction the player's sprite should be facing. 
        FacingRight = true;
        animator = GetComponent<Animator>();

        //This bool is used to check whether a certain animation should be playing, whether the ProjectileOrigin should be set to position: Ducking, and if the player's collider should shrink in correspondence with you ducking.
        Ducked = false;
        //This bool serves the same function as Ducked, except for whether you should be looking up.
        LookingUp = false;
        PlayerBoxCollider = GetComponent<BoxCollider2D>();

        JumpTimer = 0;
        JumpTimerThreshold = 0.4f;
    }

    private void ColliderInitialisation()
    {
        StoodUpColliderOffset = new Vector2(-0.0311029f, -0.1082547f);
        StoodUpColliderSize = new Vector2(0.488239f, 1.717764f);
        DuckedColliderOffset = new Vector2(-0.0311029f, -0.4876489f);
        DuckedColliderSize = new Vector2(0.488239f, 0.9589756f);
    }

    private void ProjectileOriginInitialisation()
    {
        ProjectileOriginStoodUp = new Vector3(0.562f, 0.105f);
        ProjectileOriginDucked = new Vector3(0.621f, -0.522f);
        ProjectileOriginLookUp = new Vector3(0.186f, 0.698f);
        ProjectileOrigin = GetComponentInChildren<Transform>().Find("Projectile_Origin");
        //Setting the ProjectileOrigin to it's default position
        ProjectileOrigin.transform.localPosition = ProjectileOriginStoodUp;
        DamageMultiplier = 1;
    }

    private void MovementUpdate()
    {
        //LeftorRight = Input.GetAxis("Horizontal");
        CheckPlayerState();
        AlterRunningSpeed();
        CanIJump();
        ShouldIMove();
        CheckIfIShouldFlip();
        if (RunAntiStick)
        {
            WallAntiStick();
        }
        AnimatorChecks();
    }

    public bool GetDucked
    {
        get
        {
            return Ducked;
        }
    }

    public bool GetGrounded
    {
        get
        {
            return Grounded;
        }
    }

    public bool GetLookingUp
    {
        get
        {
            return LookingUp;
        }
    }

    public float GetWalkingSpeed
    {
        get
        {
            return WalkingSpeed;
        }
    }


    public bool GetFacingRight
    {
        get
        {
            return FacingRight;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        RunAntiStick = true;
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        animator.SetBool("WallStuck", false);
        RunAntiStick = false;
    }

    private void CheckPlayerState()
    {
        //This checks if the player is currently Ducking
        if (Input.GetKeyDown(KeyBindsClass.Crouch.keyCode) && Grounded && !LookingUp)
        {
            Duck(true);
        }
        if (Input.GetKeyDown(KeyBindsClass.LookUp.keyCode) && Ducked)
        {
            Duck(false);
        }
        //This checks if the player is currently looking Up.
        else if (Input.GetKeyDown(KeyBindsClass.LookUp.keyCode) && Grounded)
        {
            //Shoot Up
            animator.SetBool("LookingUp", true);
            ProjectileOrigin.localPosition = ProjectileOriginLookUp;
            LookingUp = true;
        }

        if ((LookingUp && Input.GetAxis("Horizontal") != 0) || LookingUp && Input.GetKeyDown(KeyBindsClass.Crouch.keyCode))
        {
            animator.SetBool("LookingUp", false);
            LookingUp = false;
            ProjectileOrigin.localPosition = ProjectileOriginStoodUp;
        }
    }
    #endregion


    //All methods below here are related to Points and Damage

    #region Damage Methods
    private void DamageAndPointsPropertiesInit()
    {
        AllowedToTakeDamage = true;
        TakeDamage = true;
        animator = GetComponent<Animator>();
        rifle = new Rifle();
        shotgun = new Shotgun();
        bazooka = new Bazooka();
        sniper = new Sniper();
        Weapon = rifle;
        RateOfFire = Weapon.GetRateOfFire;
        RateOfFireTimer = 1/RateOfFire;

        //These two lines are from the old "Pickups_Script"
        Points = 0;
        Manager = GameObjectFinder.GetSetManager;
        SpeedBoostMultiplier = 1;
    }

    private void DamageAndPointsUpdate()
    {
        if (Input.GetKeyDown(KeyBindsClass.Rifle.keyCode))
        {
            Weapon = rifle;
            Manager.GetSetCurrentWeapon = Weapon;
            RateOfFire = Weapon.GetRateOfFire;
            RateOfFireTimer = 1/RateOfFire;

        }
        else if (Input.GetKeyDown(KeyBindsClass.Shotgun.keyCode))
        {
            Weapon = shotgun;
            Manager.GetSetCurrentWeapon = Weapon;
            RateOfFire = Weapon.GetRateOfFire;
            RateOfFireTimer = 1/RateOfFire;
        }
        else if (Input.GetKeyDown(KeyBindsClass.Sniper.keyCode))
        {
            Weapon = sniper;
            Manager.GetSetCurrentWeapon = Weapon;
            RateOfFire = Weapon.GetRateOfFire;
            RateOfFireTimer = 1/RateOfFire;
        }
        else if (Input.GetKeyDown(KeyBindsClass.Bazooka.keyCode))
        {
            Weapon = bazooka;
            Manager.GetSetCurrentWeapon = Weapon;
            RateOfFire = Weapon.GetRateOfFire;
            RateOfFireTimer = 1/RateOfFire;
        }

        if (((Input.GetKeyDown(KeyBindsClass.Shoot.keyCode) && Grounded == true && !Ducked) || (Input.GetKeyDown(KeyBindsClass.Shoot.keyCode) && Ducked)) && RateOfFireTimer >= 1/RateOfFire)
        {
            Shoot();
            RateOfFireTimer = 0;
            Manager.GetSetCurrentWeapon = Weapon;

        }
        else if (RateOfFireTimer <= 1/RateOfFire)
        {
            RateOfFireTimer += Time.deltaTime;
        }
    }

    private void Shoot()
    {
        //GameObject NewProjectile = Instantiate(Weapon, null, true);
        GameObject[] NewProjectiles = Weapon.InstantiateProjectile();
        animator.SetBool("Shooting", true);
        if (NewProjectiles != null)
        {
            foreach (GameObject bullet in NewProjectiles)
            {
                bullet.transform.position = ProjectileOrigin.position;

                bullet.GetComponent<Bullet_Behaviour>().GetSetDamage = Mathf.RoundToInt(Weapon.GetDamage * DamageMultiplier);
            }
        }
        
        
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.gameObject.tag == "Enemy" && AllowedToTakeDamage && collision.otherCollider == transform.GetComponent<BoxCollider2D>())
        {
            //Code something that allows you to take True
            //Play the animation (uninterruptable so you can't continually take True) and then make a method Event where you then take the True and of course make you able to die.
            if (TakeDamage)
            {
                TakeDamage = false;
                Health -= 25;
                animator.SetBool("Hit", true);
                StartCoroutine(StopTakingDamage());
                TakeDamage = true;
            }



        }
    }

    void StopShooting()
    {
        animator.SetBool("Shooting", false);
    }
    #endregion

    #region Health, Damage and Pickup Get/Setters
    public int GetSetHealth
    {
        get
        {
            return Health;
        }
        set
        {
            Health = value;
            if (Health > 100)
            {
                Health = 100;
            }
        }
    }

    public float GetSetSpeedMultiplier
    {
        get
        {
            return SpeedBoostMultiplier;
        }
        set
        {
            SpeedBoostMultiplier = value;
            StartCoroutine(ResetSpeedMultiplier());
        }
    }

    public PlayerWeapons GetCurrentWeapon
    {
        get
        {
            return Weapon;
        }
    }

    public float GetSetJumpHeight
    {
        get
        {
            return JumpingSpeed;

        }
        set
        {
            JumpingSpeed = value;
            StartCoroutine(ResetJumpHeight(JumpingSpeedInitialValue));
        }
    }
    public float GetSetDamageMultiplier
    {
        get
        {
            return DamageMultiplier;
        }
        set
        {
            DamageMultiplier = value;
            StartCoroutine(ResetDamage());
        }
    }

    //I was originally planning to have StopTakingDamage and StartTakingDamage to be the same function, just with a boolean parameter, however, unity's animation event system (for some reason) only allows functions that have no parameters
    public IEnumerator StopTakingDamage()
    {
        AllowedToTakeDamage = false;
        yield return new WaitForSeconds(0.6f);
        StartCoroutine(StartTakingDamage());
    }

    public IEnumerator StartTakingDamage()
    {
        AllowedToTakeDamage = true;
        animator.SetBool("Hit", false);
        yield return null;
    }
    #endregion

    #region Enumerators and CoRoutines
    private IEnumerator ResetSpeedMultiplier()
    {
        yield return new WaitForSeconds(10f);
        SpeedBoostMultiplier = 1;
    }

    private IEnumerator ResetJumpHeight(float SetJumpingSpeed)
    {
        yield return new WaitForSeconds(10f);
        JumpingSpeed = SetJumpingSpeed;
    }

    private IEnumerator ResetDamage()
    {
        yield return new WaitForSeconds(10f);
        DamageMultiplier = 1f;
    }

    public void RapidFireBoost(float TimeRemaining)
    {
        StartCoroutine(RapidFireBoostLoop(TimeRemaining));
    }

    private IEnumerator RapidFireBoostLoop(float TimeRemaining)
    {
        if (TimeRemaining <= 0)
        {
            yield return null;
        }
        else
        {
            float StartTime = Time.time;
            Shoot();
            yield return new WaitForSeconds(0.2f);

            StartCoroutine(RapidFireBoostLoop(TimeRemaining - (Time.time - StartTime)));
        }

    }
    #endregion



    

    private void OnGUI()
    {
        if (Input.GetKey(KeyBindsClass.MoveRight.keyCode))
        {
            if (LeftorRight < 1)
            {
                LeftorRight += Time.deltaTime*3f;
            }
        }
        else if (Input.GetKey(KeyBindsClass.MoveLeft.keyCode))
        {
            if (LeftorRight > -1)
            {
                LeftorRight -= Time.deltaTime*3f;
            }
        }
        else
        {
            if (LeftorRight> 0)
            {
                if (LeftorRight <= 0.1)
                {
                    LeftorRight = 0;
                }
                else
                {
                    LeftorRight -= Time.deltaTime*2f;
                }
            }
            if (LeftorRight < 0)
            {
                if (LeftorRight >= -0.1)
                {
                    LeftorRight = 0;
                }
                else
                {
                    LeftorRight += Time.deltaTime*2f;
                }
            }
        }
       
    }



}


