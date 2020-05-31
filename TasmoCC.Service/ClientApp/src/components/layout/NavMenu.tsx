import React from 'react';
import { Link } from 'react-router-dom';

import Nav from 'react-bootstrap/Nav';
import Navbar from 'react-bootstrap/Navbar';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faLaptopHouse, faHome, faMicrochip } from '@fortawesome/free-solid-svg-icons';

export const NavMenu = () => {
    return (
        <div>
            <Navbar bg="primary" variant="dark" expand="sm" className="mb-3 py-1">
                <Navbar.Brand as={Link} to="/">
                    <span className="mr-1">
                        <FontAwesomeIcon icon={faLaptopHouse} fixedWidth />
                    </span>
                    TasmoCC
                </Navbar.Brand>
                <Navbar.Toggle />
                <Navbar.Collapse>
                    <Nav className="mr-auto">
                        <Nav.Link as={Link} to="/">
                            <span className="mr-1">
                                <FontAwesomeIcon icon={faHome} fixedWidth />
                            </span>
                            Home
                        </Nav.Link>
                        <Nav.Link as={Link} to="/devices">
                            <span className="mr-1">
                                <FontAwesomeIcon icon={faMicrochip} fixedWidth />
                            </span>
                            Devices
                        </Nav.Link>
                    </Nav>
                </Navbar.Collapse>
            </Navbar>
        </div>
    );
};

NavMenu.displayName = NavMenu.name;
