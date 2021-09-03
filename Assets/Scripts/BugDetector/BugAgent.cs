﻿using System.Collections.Generic;
using UnityEngine;
using MLAgents;
using System;
using System.Runtime.InteropServices;
using Object = System.Object;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine.Assertions.Must;
using UnityEngine.Serialization;

public class BugAgent : Agent
{

    // Movement values of the agent.
    public float _agentSpeed = 60;
    [Range(0f, 10f)]
    public float _agentJump = 2f;
    private CharacterController _characterController;
    public float turnSmoothTime = 0.2f;
    private float turnSmoothVelocity;
    
    // Whether to use a discrete or continuous action space
    public bool _discrete = true;
    
    // ML stuff
    public BugAcademy _academy;
    List<float> observation = new List<float>();
    public int _timeScale = 15;
    private int _frameCount;
    
    // Perceptors definition
    public Horizontal2DGrid _horizontalGrid;
    public Horizontal2DGrid _globalGrid;
    public Vertical2DGrid _verticalGrid;
    public ThreeDGrid _threeDGrid;

    public float _eps = 1f;
    public float _radiusAround = 10f;

    // Movement variables.
    [HideInInspector]
    public float _horizontal;
    [HideInInspector]
    public float _vertical;
    [HideInInspector]
    public float _jump;
    
    private bool _isGrounded = false;
    private bool _doubleJump = false;
    private bool _isAttached = false;
    
    private Rigidbody _rigidbody;
    
    // For a possible target reward.
    private List<float> distances;
    
    // List of keys in the environment (if any).
    public List<Key> keys;

    // Start is called before the first frame update
    void Start()
    {
        _characterController = gameObject.GetComponent<CharacterController>();
        if (GetComponent<Rigidbody>() != null)
            _rigidbody = GetComponent<Rigidbody>();
    }

    public override void AgentReset()
    {   
        // Reset variables
        _frameCount = 0;
        _horizontal = 0;
        _vertical = 0;
        _jump = 0;
        _isGrounded = true;
        _doubleJump = false;
        _isAttached = false;
        distances = new List<float>();
        _academy.AcademyReset();

    }

    // Return the type of the object collided with a cube
    public float getTypeOfCollision(GameObject collision)
    {
        if (collision != null)
        {
            if (collision.CompareTag("Wall"))
            {
                // wall
                return 1f;
            }
            
            if (collision.CompareTag("Ramp"))
            {
                // wall
                return 1f;
            }
            
            if (collision.CompareTag("Key"))
            {
                // key
                return 3f;
            }
            
            if (collision.CompareTag("Plane"))
            {
                // wall
                return 1f;
            }
            
            // nothing
            return 0; 

        }
        
        // nothing
        return 0f;

    }
    
    // Get the local observation from 3D grid in form of local matrix.
    public void get3DGridObservation(ThreeDGrid grid, List<float> observations)
    {
        // Get the cube grid
        CubeCollision[,,] cubes = grid.gridMatrix;
        int width = grid._gridWidth;
        
        // For each cube
        for (int x = 0; x < width; x++)
            for (int y = 0; y < width; y++)
                for (int z = 0; z < width; z++)
                {
                    GameObject collision = cubes[x, y, z].getGameObject();
                    // Get the object at that position
                    
                    // In the center of the map, there is the agent.
                    if (x == Math.Ceiling((double)width / 2) - 1 &&
                        y == Math.Ceiling((double)width / 2) - 1 &&
                        z == Math.Ceiling((double)width / 2))
                    {
                        // agent
                        observations.Add(2f);
                    }
                    else
                    {
                        observations.Add(getTypeOfCollision(collision));
                    }
                }
    }

    // Instead of the type of object in the cube, return the distances of each cube that touches something.
    public void getLocal3DObservationWithDistance(int width, ThreeDGrid grid, List<float> observations)
    {
        // Get the cube grid
        CubeCollision[,,] cubes = grid.gridMatrix;

        // Populate the matrix
        // Only for coins
        for (int x = 0; x < width; x++)
            for (int y = 0; y < width; y++)
                for (int z = 0; z < width; z++)
                {
                    // Get the object at that position
                    GameObject collision = cubes[x, y, z].getGameObject();
                    if (collision != null)
                    {
                        observations.Add(cubes[x, y, z].GetComponent<CubeCollision>().distance);
                    }
                    else
                    {
                        observations.Add(0f);
                    }
                }
    }

       // Get the local observation from 3D grid in form of local matrix.
    public void get2DGridObservation(Horizontal2DGrid grid, List<float> observations)
    {
        // Get the cube grid
        CubeCollision[,] cubes = grid.gridMatrix;
        int width = grid._gridWidth;
        
        // For each cube
        for (int x = 0; x < width; x++)
            for (int y = 0; y < width; y++)
            {
                    GameObject collision = cubes[x, y].getGameObject();
                    // Get the object at that position
                    
                    // In the center of the map, there is the agent.
                    if (x == Math.Ceiling((double)width / 2) - 1 &&
                        y == Math.Ceiling((double)width / 2) - 1)
                    {
                        // agent
                        observations.Add(2f);
                    }
                    else
                    {
                        observations.Add(getTypeOfCollision(collision));
                    }
            }
    }

    public override void CollectObservations()
    {

        observation = new List<float>();
        // Get agent position
        Vector3 agentPosition = gameObject.transform.position;
        

        // Normalize Agent position
        float agentX;
        float agentZ;
        float agentY; 
        
        agentX = normalize(agentPosition.x, 250, -250);
        agentZ = normalize(agentPosition.z, 250, -250);
        agentY = normalize(agentPosition.y, 40, 1);


        // If the agent is grounded or not
        float isGrounded = agentPosition.y == 1f ? 1f : 0f;
        // If the agent can do a double jump 
        float canDoubleJump = _doubleJump ? 1f : 0f;

        // Add obs
        observation.Add(agentX);
        observation.Add(agentZ);
        observation.Add(agentY);
        observation.Add(isGrounded);
        observation.Add(canDoubleJump);
        
        // If we use a global grid, add the relative observations.
        if (_globalGrid != null)
        {
            get2DGridObservation(_globalGrid, observation);
        }

        // If we use a 2D horizontal grid, add the relative observations.
        if (_horizontalGrid != null)
        {
            get2DGridObservation(_horizontalGrid, observation);
        }

        // If we use a 2D vertical grid, add the relative observations.
        if (_verticalGrid != null)
        {
            get2DGridObservation(_verticalGrid, observation);
        }

        if (_threeDGrid != null)
        {
            get3DGridObservation(_threeDGrid, observation);
        }

        AddVectorObs(observation);

    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.collider.gameObject.CompareTag("Plane") || other.collider.gameObject.CompareTag("Ramp") ||
            other.collider.gameObject.CompareTag("Wall") || other.collider.gameObject.CompareTag("OnlyPlane"))
        {
            ContactPoint contact = other.contacts[0];
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.5)
            {
                _isGrounded = true;
                _doubleJump = true;
            }

        }
    }

    private void OnCollisionStay(Collision other)
    {
        if (other.collider.gameObject.CompareTag("Plane") || other.collider.gameObject.CompareTag("Ramp") ||
            other.collider.gameObject.CompareTag("Wall") || other.collider.gameObject.CompareTag("OnlyPlane"))
        {
            ContactPoint contact = other.contacts[0];
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.5)
            {
                _isGrounded = true;
                _doubleJump = true;
            }

        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Glitch"))
        {
            _doubleJump = true;
        }

        if (other.gameObject.CompareTag("Climbable"))
        {
            _isAttached = true;
            _doubleJump = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Climbable"))
        {
            _isAttached = false;
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (other.collider.gameObject.CompareTag("Plane") || other.collider.gameObject.CompareTag("Ramp") ||
            other.collider.gameObject.CompareTag("Wall") || other.collider.gameObject.CompareTag("OnlyPlane"))
        {
            _isGrounded = false;

        }
    }

    public void DiscreteMovement(int action)
    {
        float horizontal = 0;
        float vertical = 0;
        float jump = 0;
        switch (action)
        {
            case 1:
                horizontal = 1;
                vertical = 0;
                break;
            case 2:
                horizontal = -1;
                vertical = 0;
                break;
            case 3:
                horizontal = 0;
                vertical = 1;
                break;
            case 4:
                horizontal = 0;
                vertical = -1;
                break;
            case 5:
                horizontal = 1;
                vertical = 1;
                break;
            case 6:
                horizontal = -1;
                vertical = -1;
                break;
            case 7:
                horizontal = 1;
                vertical = -1;
                break;
            case 8:
                horizontal = -1;
                vertical = 1;
                break;
            case 9:
                if (_isGrounded || (!_isGrounded && _doubleJump))
                {
                    jump = 0.8f;
                }
                if (!_isGrounded && _doubleJump)
                    _doubleJump = false;

                horizontal = 0;
                vertical = 0;
                break;
            case 0:
                horizontal = 0;
                vertical = 0;
                break;
            default:
                horizontal = 0;
                vertical = 0;
                break;
        }

        _horizontal = horizontal;
        _vertical = vertical;
        _jump = jump;
    }

    public override void AgentAction(float[] vectorAction, string textAction)
    {
        if (!_discrete)
        {
            _horizontal = vectorAction[0];
            _vertical = vectorAction[1];
        }
        else
        {
            DiscreteMovement((int)vectorAction[0]);
        }
    }
    
    // Target reward, if any. It is based on Alonso et al. https://arxiv.org/pdf/2011.04764.pdf
    public float computeTargetReward(float distance, float eps)
    {
        if (distance < eps)
        {
            Done();
            return 1f;
        }

        float reward = 0;
        distance = normalize1(distance, 320, 0);
        if (distances.Count > 0)
            reward = distances.Min();
        reward = Math.Max(reward - distance, 0);
        distances.Add(distance);
        // Debug.Log(reward);
        // Add negative reward dependent on the distance to the target
        //return - 1f * distance;
        return reward;
    }
    
    // Movement of the agent baes on its rigidBody
    public void Movement(float horizontal, float vertical, float jump)
    {
        if (_isAttached && _rigidbody.velocity.y < 0f)
        {
            _rigidbody.velocity = new Vector3(_rigidbody.velocity.x, 0, _rigidbody.velocity.z);
        }
        if (jump > 0)
        {
            _rigidbody.velocity = new Vector3(0, 0, 0);
            _rigidbody.AddForce(Vector3.up * Mathf.Sqrt(_agentJump * -2f * Physics.gravity.y), ForceMode.Impulse);
            _jump = 0;
            _isGrounded = false;
        }
        Vector3 direction = new Vector3(horizontal, 0, vertical);
        if (direction.magnitude < 0.2f)
            return;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
        //transform.forward = direction;
        //transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        if (_rigidbody != null)
        {
            direction = direction.normalized * _agentSpeed * Time.fixedDeltaTime;
            direction.y = direction.y * 5;
            _rigidbody.MovePosition(_rigidbody.position + direction.normalized * _agentSpeed * Time.fixedDeltaTime);
        }
        else
        {
            _characterController.Move(moveDir.normalized * _agentSpeed * Time.fixedDeltaTime);
        }

    }
    
    // Get the objects around the player
    public Collider[] getObjectsAround()
    {
        int envObjectsLayer = 1 << 9;
        return Physics.OverlapSphere(transform.position, _radiusAround, envObjectsLayer);
    }

    // Normalize between -1 and 1
    public float normalize(float value, float max, float min)
    {
        return (2 * ((value - min) / (max - min)) - 1);
    }

    // Normalize between 0 and 1
    public float normalize1(float value, float max, float min)
    {
        return (value - min) / (max - min);
    }

    void FixedUpdate()
    {

        if (_frameCount % _timeScale == 0)
        {
            // Request a decision every _timeScale frames. The reward is given directly by the cubeRewards (if any).
            RequestDecision();
        }

        _frameCount++;

        Movement(_horizontal, _vertical, _jump);
    }

    // Get last action distribution probabilities
    public float[] getLastProbabilitiesDistribution()
    {
        float[] probs = new float[9];
        for (int i = 0; i < 9; i++)
        {
            probs[i] = lastProbabilitiesDistribution[0, i];
        }

        return probs;
    }

    public float[] probsToLogits(float[] probs)
    {
        float[] logits = new float[probs.Length];

        for (int i = 0; i < probs.Length; i++)
        {
            logits[i] = (float)Math.Log(probs[i]);
        }

        return logits;
    }

    public float[] logitsToProbs(float[] logits, float temperature)
    {
        float[] probs = new float[logits.Length];
        float sum = 0;
        foreach (float l in logits)
        {
            sum += (float)Math.Exp(l / temperature);
        }

        for (int i = 0; i < logits.Length; i++)
        {
            probs[i] = (float)Math.Exp(logits[i] / temperature) / sum;
        }

        return probs;
    }

    // Compute entropy of a probability distribution
    public float getProbsEntopry(float[] probs)
    {
        float entr = 0;

        foreach (float p in probs)
        {
            entr += p * (float)Math.Log(p + 1e-5);
        }

        return -entr;
    }

    public int sampleActionProbabilities(int numToConsider, float temperature, float[] probs)
    {
        int action = 0;

        // Get logits for temperature factor
        float[] logits = probsToLogits(probs);

        // Get probs with temperature factor
        probs = logitsToProbs(logits, temperature);

        // Define dictionary <action, probs>
        Dictionary<int, float> actionProbs = new Dictionary<int, float>();
        for (int i = 0; i < probs.Length; i++)
        {
            actionProbs.Add(i, probs[i]);
        }

        // Put the probability of the actions last NumAction - numToConsider
        // to 0, as we want to consider only the most probable numToConsider
        // action. We therefore compute the cumulative sum.
        int count = 0;
        float cumulativeSum = 0f;
        foreach (KeyValuePair<int, float> actionProb in actionProbs.OrderBy(key => key.Value))
        {
            if (count < probs.Length - numToConsider)
            {
                actionProbs[actionProb.Key] = 0f;
            }
            cumulativeSum += actionProbs[actionProb.Key];
            count++;
        }

        // Choose a random number between 0 and the cumulative sum
        float r = UnityEngine.Random.Range(0.00001f, cumulativeSum);
        float sum = 0f;
        // For each action in probability order
        foreach (KeyValuePair<int, float> actionProb in actionProbs.OrderBy(key => key.Value))
        {
            // Add to sum action prob
            sum += actionProb.Value;
            // If the random number is less then the sum at this point
            if (r <= sum)
            {
                // This is the action
                action = actionProb.Key;
                break;
            }
        }

        return action;
    }


}